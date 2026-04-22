# Promo Radar

Aplicação SaaS em **ASP.NET Core 9 MVC** para monitoramento de preços de peças de PC no Brasil, com dashboard premium claro, autenticação com Identity, persistência em PostgreSQL e agendamentos com Hangfire.

## Stack

- ASP.NET Core 9
- MVC + Razor Views
- Bootstrap 5 + CSS customizado
- PostgreSQL
- Entity Framework Core
- ASP.NET Identity
- Hangfire

## Estrutura

- `PromoRadar.sln`
- `PromoRadar.Web/Controllers`
- `PromoRadar.Web/Models`
- `PromoRadar.Web/Data`
- `PromoRadar.Web/Services`
- `PromoRadar.Web/ViewModels`
- `PromoRadar.Web/Views`
- `PromoRadar.Web/wwwroot/css`
- `PromoRadar.Web/wwwroot/js`
- `PromoRadar.Web/Configurations`

## Pré-requisitos

- .NET SDK 9.0+
- PostgreSQL 15+ (ou compatível)
- Visual Studio 2022+ com workload ASP.NET e Desenvolvimento .NET
- Ferramenta EF instalada: `dotnet-ef`

## Connection string (Development)

Arquivo: `PromoRadar.Web/appsettings.Development.json`

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=promoradar_dev;Username=postgres;Password=postgres"
}
```

Se seu PostgreSQL usa outra senha/usuário, ajuste esse valor antes de executar.

## Executar via CLI

### 1) Restaurar pacotes

```powershell
dotnet restore PromoRadar.sln
```

### 2) Build

```powershell
dotnet build PromoRadar.sln -c Debug
```

### 3) Criar migration (quando necessário)

> A migration inicial já está versionada no projeto (`Data/Migrations`).

```powershell
dotnet ef migrations add NomeDaMigration --project PromoRadar.Web/PromoRadar.Web.csproj --startup-project PromoRadar.Web/PromoRadar.Web.csproj --output-dir Data/Migrations
```

### 4) Aplicar banco

```powershell
dotnet ef database update --project PromoRadar.Web/PromoRadar.Web.csproj --startup-project PromoRadar.Web/PromoRadar.Web.csproj
```

### 5) Rodar aplicação

```powershell
dotnet run --project PromoRadar.Web/PromoRadar.Web.csproj
```

## Executar no Visual Studio

1. Abra `PromoRadar.sln`.
2. Defina `PromoRadar.Web` como Startup Project.
3. Confirme a connection string em `appsettings.Development.json`.
4. Abra **Tools > NuGet Package Manager > Package Manager Console**.
5. Rode:

```powershell
Update-Database
```

6. Pressione `F5`.

## Credenciais do usuário demo

- E-mail: `luiz@promoradar.local`
- Senha: `PromoRadar@123`

Essas credenciais são criadas automaticamente pelo `DataSeeder` quando o banco está vazio.

## Rotas úteis

- Login: `/Identity/Account/Login`
- Dashboard: `/` (exige login)
- Hangfire: `/hangfire` (exige login)

## Observações técnicas

- `SuggestionItem` **não é entidade persistida** nesta etapa. Ele é montado como projeção no `DashboardService` e exposto via `SuggestionItemViewModel`.
- A Home usa dados reais de seed para widgets, cards e gráficos (Chart.js).
- O startup aplica migrations e seed automaticamente quando a conexão com o PostgreSQL está válida.
