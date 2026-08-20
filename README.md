# myHome — Joint Finance Management

> **Working name.** Still a placeholder. It lives in one MSBuild property
> (`ProductPrefix` in `backend/Directory.Build.props`) and one npm scope (`@myhome`),
> so changing it again stays cheap.

A household finance tool designed from day one for **two people who share an economy without
fully merging it**: income landing in different pockets, expenses split by different rules
depending on the concept, and goals that are sometimes joint and sometimes individual.

## Getting started

Full instructions in **[SETUP.md](SETUP.md)**. The short version, after installing Git, Node 24,
the .NET 10 SDK and Docker Desktop:

```bash
dotnet dev-certs https --trust
```

```bash
npm install
```

```bash
dotnet run --project backend/src/AppHost
```

That last command starts PostgreSQL, the API and the web app together, with the Aspire
dashboard on top of all three.
