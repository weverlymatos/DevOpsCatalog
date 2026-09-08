# DevOpsCatalog

API REST de catálogo de produtos em .NET 10 usada como laboratório de DevOps:
a mesma aplicação é executada localmente, em contêiner, em Kubernetes e provisionada
com Terraform, com pipeline de CI no GitHub Actions.

## Stack

| Camada | Tecnologia |
| --- | --- |
| API | ASP.NET Core 10 (Minimal APIs) |
| Persistência | PostgreSQL 17 + EF Core 10 (Npgsql) |
| Cache | Redis 7 (`IDistributedCache`) |
| Observabilidade | Health Checks (`live` / `ready`) + OpenAPI |
| Testes | xUnit |
| Contêiner | Docker + Docker Compose |
| Orquestração | Kubernetes (Docker Desktop) |
| IaC | Terraform (provider `hashicorp/kubernetes`) |
| CI | GitHub Actions |

## Estrutura

```
.
├── src/DevOpsCatalog.Api/     # API: Domain, Endpoints, Infrastructure, Migrations
├── tests/DevOpsCatalog.Tests/ # Testes xUnit
├── k8s/                       # Manifests Kubernetes (namespace devops-catalog)
├── terraform/                 # Lab de IaC (namespace + ConfigMap)
├── .github/workflows/ci.yml   # Build/Test, Docker Build, Terraform Validate
├── Dockerfile                 # Build multi-stage (SDK 10 → ASP.NET 10)
└── docker-compose.yml         # API + Postgres + Redis
```

## Endpoints

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/` | Status do serviço (nome, ambiente, timestamp) |
| `GET` | `/health` | Todos os health checks |
| `GET` | `/health/live` | Liveness (apenas `self`) |
| `GET` | `/health/ready` | Readiness (Postgres + Redis) |
| `GET` | `/openapi/v1.json` | Documento OpenAPI |
| `GET` | `/products` | Lista produtos (cache Redis de 5 min) |
| `GET` | `/products/{id}` | Busca por id |
| `POST` | `/products` | Cria produto (`{ "name", "price" }`) |
| `PUT` | `/products/{id}` | Atualiza produto |
| `DELETE` | `/products/{id}` | Remove produto |

Escritas (`POST`/`PUT`/`DELETE`) invalidam a chave de cache `products:all`.

## Executando

### 1. Docker Compose (caminho mais curto)

```bash
docker compose up -d --build
```

| Serviço | Porta no host |
| --- | --- |
| API | http://localhost:5080 |
| PostgreSQL | 5433 |
| Redis | 6380 |

As portas de Postgres e Redis são deslocadas de propósito (5433/6380) para não
conflitarem com instâncias já instaladas na máquina.

### 2. Local (`dotnet run`) com dependências em contêiner

```bash
docker compose up -d postgres redis
dotnet run --project src/DevOpsCatalog.Api
```

O perfil `Development` já aponta para `localhost:5433` e `localhost:6380`
(`appsettings.Development.json`).

### Migrations

A aplicação **não** aplica migrations no startup — rode explicitamente:

```bash
dotnet tool install --global dotnet-ef        # apenas uma vez
dotnet ef database update --project src/DevOpsCatalog.Api
```

### Testes

```bash
dotnet test DevOpsCatalog.slnx
```

## Kubernetes

Os manifests assumem um cluster local (Docker Desktop) usando a imagem construída
na máquina — o Deployment usa `imagePullPolicy: Never` e a tag `1.1.0`:

```bash
docker build -t devops-catalog-api:1.1.0 .
```

`k8s/secret.yaml` e `k8s/postgres-secret.yaml` estão no `.gitignore` e precisam ser
criados antes do apply:

```yaml
# k8s/secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: devops-catalog-secret
  namespace: devops-catalog
type: Opaque
stringData:
  ConnectionStrings__Postgres: "Host=devops-catalog-postgres;Port=5432;Database=devopscatalog;Username=devopscatalog;Password=devopscatalog;GSS Encryption Mode=Disable"
  ConnectionStrings__Redis: "devops-catalog-redis:6379"
```

```yaml
# k8s/postgres-secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: devops-catalog-postgres-secret
  namespace: devops-catalog
type: Opaque
stringData:
  POSTGRES_DB: "devopscatalog"
  POSTGRES_USER: "devopscatalog"
  POSTGRES_PASSWORD: "devopscatalog"
```

Deploy:

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/
kubectl get pods -n devops-catalog -w
```

Os Services são `ClusterIP`, então o acesso é via port-forward:

```bash
kubectl port-forward -n devops-catalog svc/devops-catalog-api 8080:80
curl http://localhost:8080/health/ready
```

Topologia: API (`Deployment`, 2 réplicas) → PostgreSQL (`StatefulSet` + PVC de 1Gi,
Service headless) e Redis (`Deployment` + Service ClusterIP). Probes de readiness e
liveness da API apontam para `/health/ready` e `/health/live`.

## Terraform

Lab de IaC que cria um namespace e um ConfigMap no cluster local. O provider está
fixado no contexto `docker-desktop` (`terraform/providers.tf`).

```bash
cd terraform
terraform init
terraform plan
terraform apply
```

Variáveis (`terraform.tfvars`): `namespace`, `environment`, `project_name`.

## CI

`.github/workflows/ci.yml` roda em push e PR para `main`:

- **Build and Test** — `dotnet restore/build/test` da solution em Release
- **Docker Build** — `docker build` da imagem (depende do job anterior)
- **Terraform Validate** — `terraform init -backend=false`, `fmt -check` e `validate`

## Configuração

Connection strings vêm da configuração padrão do ASP.NET Core e são obrigatórias —
a aplicação falha no startup se `ConnectionStrings:Postgres` ou
`ConnectionStrings:Redis` não estiverem definidas.

| Variável de ambiente | Exemplo |
| --- | --- |
| `ConnectionStrings__Postgres` | `Host=postgres;Port=5432;Database=devopscatalog;Username=devopscatalog;Password=devopscatalog` |
| `ConnectionStrings__Redis` | `redis:6379` |
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ASPNETCORE_URLS` | `http://+:8080` |

> As credenciais neste repositório são de laboratório local. Em qualquer ambiente
> real, use um gerenciador de segredos.

## Pré-requisitos

- .NET SDK 10
- Docker Desktop (com Kubernetes habilitado para a parte de k8s/Terraform)
- Terraform >= 1.8
