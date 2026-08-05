using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Core;

namespace Fairbill.Tests;

public sealed class JavaScriptAnalyzerTests
{
    [Fact]
    public async Task PipelineProducesDeterministicWorkspaceFrameworkAndSourceEvidence()
    {
        JavaScriptFixtureRepository repository = JavaScriptFixtureRepository.Create();
        RepositoryAnalysisPipeline pipeline = new(repository);

        RepositoryEvidence first = await pipeline.ScanAsync(repository.RootPath);
        RepositoryEvidence second = await pipeline.ScanAsync(repository.RootPath);
        string json = ContractJson.Serialize(first);

        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Empty(ContractValidation.Validate(first));
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.DoesNotContain("fixture-command-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-route-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-source-secret", json, StringComparison.Ordinal);

        EvidenceFact repositoryFact = Fact(first, "javascript:repository");
        Assert.Equal(4m, Measurement(repositoryFact, "packages"));
        Assert.Contains("package-manager:pnpm", repositoryFact.Tags);
        Assert.Contains("script-bodies:not-emitted", repositoryFact.Tags);

        EvidenceFact workspace = Fact(first, "javascript:workspace:package.json");
        Assert.Equal(3m, Measurement(workspace, "matched-packages"));
        EvidenceFact apiPackage = Fact(first, "javascript:package:apps/api/package.json");
        Assert.Contains("package-role:server", apiPackage.Tags);
        Assert.Contains("technology:express", apiPackage.Tags);
        EvidenceFact webPackage = Fact(first, "javascript:package:apps/web/package.json");
        Assert.Contains("package-role:full-stack-web", webPackage.Tags);
        Assert.Contains("technology:next", webPackage.Tags);
        Assert.Contains(first.Facts, fact =>
            fact.Id == "javascript:project-reference:apps/api/package.json:packages/shared/package.json:runtime");

        EvidenceFact rootConfiguration = Fact(
            first,
            "javascript:configuration:tsconfig.json");
        Assert.Contains("compiler-option:strict=true", rootConfiguration.Tags);
        Assert.Contains(first.Facts, fact =>
            fact.Id == "javascript:configuration-reference:tsconfig.json:apps/web/tsconfig.json:project-reference");

        EvidenceFact api = Fact(first, "javascript:api:apps/api/src/server.ts");
        Assert.Equal(2m, Measurement(api, "endpoints"));
        Assert.Contains("http-method:get", api.Tags);
        Assert.Contains("http-method:post", api.Tags);
        Assert.Contains(first.Facts, fact => fact.Id == "javascript:data:apps/api/src/server.ts");
        Assert.Contains(first.Facts, fact => fact.Id == "javascript:integration:apps/api/src/server.ts");
        Assert.Contains(first.Facts, fact => fact.Id == "javascript:security:apps/api/src/server.ts");
        Assert.Contains(first.Facts, fact => fact.Id == "javascript:validation:apps/api/src/server.ts");
        Assert.Contains(first.Facts, fact => fact.Id == "javascript:background:apps/api/src/server.ts");

        EvidenceFact page = Fact(first, "javascript:ui:apps/web/app/page.tsx");
        Assert.Equal(1m, Measurement(page, "pages"));
        Assert.True(Measurement(page, "jsx-elements") >= 1m);
        EvidenceFact jsStructure = Fact(
            first,
            "javascript:source-structure:packages/shared");
        Assert.Equal(1m, Measurement(jsStructure, "parser-backed-files"));
        EvidenceFact tsStructure = Fact(first, "javascript:source-structure:apps/api");
        Assert.True(Measurement(tsStructure, "token-backed-files") >= 1m);
        Assert.Equal(1m, Measurement(tsStructure, "interfaces"));
        EvidenceFact webAssets = Fact(first, "javascript:ui-assets:apps/web");
        Assert.Equal(1m, Measurement(webAssets, "style-files"));

        EvidenceFact unitTest = Fact(
            first,
            "javascript:test:apps/api/test/server.test.ts");
        Assert.Contains("test-type:integration", unitTest.Tags);
        EvidenceFact componentTest = Fact(
            first,
            "javascript:test:apps/web/tests/page.test.tsx");
        Assert.Contains("test-type:end-to-end", componentTest.Tags);

        EvidenceFact coverage = Fact(first, "javascript:coverage:apps/api/package.json");
        Assert.Equal(EvidenceSourceKind.DeclaredAssumed, coverage.Provenance.SourceKind);
        Assert.Equal(100m, Measurement(coverage, "lines"));
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Code == "FB4000");
        Assert.DoesNotContain(first.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task TypeScriptOnlyRepositoryRunsWithoutPackageManifest()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "tsconfig.json",
            "{ // JSONC is accepted\n \"compilerOptions\": { \"strict\": true, },\n}\n");
        repository.WriteText(
            "src/index.ts",
            "export const answer: number = 42;\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Repository.Ecosystems, ecosystem => ecosystem == "typescript");
        Assert.Contains(evidence.Facts, fact => fact.Id == "javascript:repository");
        EvidenceFact structure = Fact(evidence, "javascript:source-structure:.");
        Assert.Equal(1m, Measurement(structure, "token-backed-files"));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB4000");
    }

    [Fact]
    public async Task InvalidJavaScriptFallsBackToTokensWithoutStoppingAnalysis()
    {
        InMemoryRepository repository = new();
        repository.WriteText("package.json", "{ \"name\": \"fallback-fixture\" }\n");
        repository.WriteText(
            "src/broken.js",
            "export function broken( { return fetch('fixture-source-secret');\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB4102");
        EvidenceFact structure = Fact(evidence, "javascript:source-structure:.");
        Assert.Equal(1m, Measurement(structure, "token-backed-files"));
        Assert.Contains(evidence.Facts, fact =>
            fact.Id == "javascript:integration:src/broken.js");
    }

    [Fact]
    public async Task OutsideConfigurationReferencesAreRedacted()
    {
        InMemoryRepository repository = new();
        string outsidePath = Path.GetFullPath(Path.Combine(
            repository.RootPath,
            "..",
            "private-client",
            "tsconfig.json"));
        string escapedPath = outsidePath.Replace("\\", "\\\\", StringComparison.Ordinal);
        repository.WriteText(
            "tsconfig.json",
            $"{{ \"extends\": \"{escapedPath}\" }}\n");
        repository.WriteText("src/index.ts", "export const value = 1;\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        string json = ContractJson.Serialize(evidence);

        Assert.DoesNotContain(outsidePath, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB4011");
    }

    [Fact]
    public async Task MixedRepositoryRunsBothStaticAnalyzers()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Api.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText(
            "Program.cs",
            "var app = WebApplication.Create(); app.MapGet(\"/fixture-route-secret\", () => true); app.Run();\n");
        repository.WriteText(
            "web/package.json",
            "{ \"name\": \"web-fixture\", \"dependencies\": { \"react\": \"19.0.0\" } }\n");
        repository.WriteText(
            "web/index.jsx",
            "export function App() { return <main>fixture-source-secret</main>; }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Facts, fact => fact.Id == "dotnet:repository");
        Assert.Contains(evidence.Facts, fact => fact.Id == "javascript:repository");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB3000");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB4000");
    }

    [Fact]
    public async Task VueAndSvelteComponentsAreAnalyzedWithoutExecutingFrameworkTools()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "src/Counter.vue",
            """
            <script setup lang="ts">
            import { ref } from "vue";
            const count = ref<number>(0);
            </script>
            <template><button>{{ count }}</button></template>
            """);
        repository.WriteText(
            "src/Badge.svelte",
            """
            <script>
            export let label;
            </script>
            <strong>{label}</strong>
            """);

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Repository.Ecosystems, ecosystem => ecosystem == "javascript");
        Assert.Contains(evidence.Facts, fact => fact.Id == "javascript:ui:src/Counter.vue");
        Assert.Contains(evidence.Facts, fact => fact.Id == "javascript:ui:src/Badge.svelte");
        EvidenceFact structure = Fact(evidence, "javascript:source-structure:.");
        Assert.Equal(2m, Measurement(structure, "token-backed-files"));
    }

    [Fact]
    public async Task GeneratedAndMinifiedSourcesDoNotProduceSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("package.json", "{ \"name\": \"exclusions-fixture\" }\n");
        repository.WriteText(
            "src/routes.generated.ts",
            "const app = {}; app.get('/private-route', () => true);\n");
        repository.WriteText(
            "public/client.min.js",
            "const app={};app.post('/private-route',()=>true);\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Id == "javascript:api:src/routes.generated.ts" ||
            fact.Id == "javascript:api:public/client.min.js");
        Assert.Contains(evidence.Facts, fact => fact.Id == "excluded-content:generated");
        Assert.Contains(evidence.Facts, fact => fact.Id == "excluded-content:minified");
    }

    private static EvidenceFact Fact(RepositoryEvidence evidence, string id) =>
        Assert.Single(evidence.Facts, fact => fact.Id == id);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private sealed class JavaScriptFixtureRepository : InMemoryRepository
    {
        public static JavaScriptFixtureRepository Create()
        {
            JavaScriptFixtureRepository repository = new();
            repository.WriteText(
                "package.json",
                """
                {
                  "name": "workspace-fixture",
                  "private": true,
                  "packageManager": "pnpm@10.0.0",
                  "workspaces": ["apps/*", "packages/*"],
                  "scripts": {
                    "build": "turbo run build --token fixture-command-secret"
                  },
                  "devDependencies": {
                    "turbo": "2.5.0",
                    "typescript": "5.9.0"
                  }
                }
                """);
            repository.WriteText(
                "pnpm-workspace.yaml",
                "packages:\n  - 'apps/*'\n  - 'packages/*'\n");
            repository.WriteText("pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
            repository.WriteText(
                "tsconfig.json",
                """
                {
                  // project references remain static
                  "compilerOptions": {
                    "strict": true,
                    "target": "ES2025",
                  },
                  "references": [
                    { "path": "./apps/web" },
                    { "path": "./apps/api" },
                  ],
                }
                """);
            repository.WriteText(
                "apps/web/package.json",
                """
                {
                  "name": "@fixture/web",
                  "private": true,
                  "scripts": { "test": "vitest", "e2e": "playwright test" },
                  "dependencies": {
                    "next": "16.0.0",
                    "react": "19.0.0",
                    "zod": "4.0.0"
                  },
                  "devDependencies": {
                    "vitest": "3.0.0",
                    "@testing-library/react": "16.0.0",
                    "@playwright/test": "1.55.0"
                  }
                }
                """);
            repository.WriteText(
                "apps/web/tsconfig.json",
                "{ \"extends\": \"../../tsconfig.json\", \"compilerOptions\": { \"jsx\": \"preserve\" } }\n");
            repository.WriteText(
                "apps/web/app/page.tsx",
                """
                import { useEffect, useState } from "react";
                import { z } from "zod";

                export default function Page(): JSX.Element {
                  const [count, setCount] = useState<number>(0);
                  useEffect(() => setCount(1), []);
                  z.object({ count: z.number() }).safeParse({ count });
                  return <form><button>{count}</button><span>fixture-source-secret</span></form>;
                }
                """);
            repository.WriteText(
                "apps/web/tests/page.test.tsx",
                """
                import { test, expect } from "@playwright/test";
                test("page", async ({ page }) => {
                  await page.goto("/fixture-route-secret");
                  await expect(page.locator("main")).toBeVisible();
                });
                """);
            repository.WriteText(
                "apps/api/package.json",
                """
                {
                  "name": "@fixture/api",
                  "private": true,
                  "dependencies": {
                    "@fairbill/shared": "workspace:*",
                    "@prisma/client": "6.0.0",
                    "bullmq": "5.0.0",
                    "express": "5.0.0",
                    "passport": "0.7.0",
                    "stripe": "18.0.0",
                    "zod": "4.0.0"
                  },
                  "devDependencies": {
                    "jest": "30.0.0",
                    "supertest": "7.0.0"
                  },
                  "jest": {
                    "coverageThreshold": {
                      "global": { "lines": 100, "branches": 80, "functions": 90, "statements": 100 }
                    }
                  }
                }
                """);
            repository.WriteText(
                "apps/api/tsconfig.json",
                "{ \"extends\": \"../../tsconfig.json\", \"compilerOptions\": { \"noEmit\": true } }\n");
            repository.WriteText(
                "apps/api/src/server.ts",
                """
                import express from "express";
                import passport from "passport";
                import Stripe from "stripe";
                import { PrismaClient } from "@prisma/client";
                import { Queue } from "bullmq";
                import { z } from "zod";

                interface OrderRequest { id: string; }
                const app = express();
                const prisma = new PrismaClient();
                const stripe = new Stripe("fixture-source-secret");
                const queue = new Queue("billing");
                app.get("/fixture-route-secret", async () => prisma.order.findMany());
                app.post("/orders", passport.authenticate("jwt"), async () => {
                  z.object({ id: z.string() }).safeParse({ id: "1" });
                  await stripe.paymentIntents.create({ amount: 100, currency: "usd" });
                  await queue.add("invoice", {});
                });
                export default app;
                """);
            repository.WriteText(
                "apps/web/app/page.css",
                ".page { display: grid; gap: 1rem; }\n");
            repository.WriteText(
                "apps/api/test/server.test.ts",
                """
                import request from "supertest";
                import { describe, expect, test } from "@jest/globals";
                describe("api", () => {
                  test("works", async () => expect((await request({}).get("/orders")).status).toBe(200));
                });
                """);
            repository.WriteText(
                "packages/shared/package.json",
                """
                {
                  "name": "@fairbill/shared",
                  "version": "1.0.0",
                  "exports": "./src/index.jsx",
                  "dependencies": { "react": "19.0.0" }
                }
                """);
            repository.WriteText(
                "packages/shared/src/index.jsx",
                """
                import React from "react";
                const matcher = /^(a+)+$/u;
                export function Badge({ children }) {
                  return matcher.test(String(children)) ? <strong>{children}</strong> : null;
                }
                """);
            return repository;
        }
    }
}
