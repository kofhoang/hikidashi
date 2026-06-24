# infra — one-time Scaleway provisioning (Terraform)

Provisions the **reusable infrastructure** for hikidashi: the container registry namespace, the
Serverless Containers namespace, the Serverless SQL database, and an IAM application + API key the
app uses as its database login. It does **not** create the container itself — the CI deploy pipeline
(`deploy.sh` / `.github/workflows/ci.yml`) does that on every push to `main`.

Run this once (and again only when infra changes). It's the bootstrap that produces the secrets CI
needs; CI can't create them from nothing.

## Prerequisites
- [Terraform](https://developer.hashicorp.com/terraform/install) ≥ 1.5
- A Scaleway API key with admin rights, exported for the provider:
  ```sh
  export SCW_ACCESS_KEY=...        SCW_SECRET_KEY=...
  export SCW_DEFAULT_PROJECT_ID=...  SCW_DEFAULT_ORGANIZATION_ID=...
  ```

## Apply
```sh
cd infra
terraform init
terraform apply -var "project_id=$SCW_DEFAULT_PROJECT_ID"
```

## Wire the outputs into GitHub (so CI can deploy)
```sh
gh secret set SCW_ACCESS_KEY          --body "$SCW_ACCESS_KEY"
gh secret set SCW_SECRET_KEY          --body "$SCW_SECRET_KEY"
gh secret set SCW_DEFAULT_PROJECT_ID  --body "$SCW_DEFAULT_PROJECT_ID"
gh secret set DATABASE_URL            --body "$(terraform output -raw database_url)"
gh secret set AUTH_AUTHORITY          --body "https://<your-project>.authkit.app"   # from WorkOS (SETUP.md Part B)
gh secret set AUTH_AUDIENCE           --body "https://<endpoint>/mcp"               # known after the first deploy
```

Then enable the deploy job (it's dormant until this is set, so CI stays green test-only until you're ready):
```sh
gh variable set DEPLOY_ENABLED --body true
```

`AUTH_AUDIENCE` is chicken-and-egg: set a placeholder, let CI deploy once to learn the container
endpoint, then set the real `https://<endpoint>/mcp` (and the matching WorkOS Resource Indicator)
and re-run the pipeline. See [`../SETUP.md`](../SETUP.md) Part D step 6.

## Caveats (unverified against a live account)
- Resource and attribute names follow the current `scaleway/scaleway` provider; if `terraform
  validate`/`plan` complains, check the
  [provider docs](https://registry.terraform.io/providers/scaleway/scaleway/latest/docs).
- `db_permission_set` defaults to a guessed name — confirm with `scw iam permission-set list | grep -i sql`.
- The DB **username** is set to the IAM application ID; if connections are rejected, try the API
  **access key** instead (see the comment in `main.tf`).
