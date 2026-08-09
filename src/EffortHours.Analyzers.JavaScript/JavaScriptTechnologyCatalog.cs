using System.Collections.Frozen;

namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptTechnologyCatalog
{
    private static readonly FrozenDictionary<string, TechnologyClassification> Packages =
        new Dictionary<string, TechnologyClassification>(StringComparer.OrdinalIgnoreCase)
        {
            ["react"] = new("react", "ui"),
            ["react-dom"] = new("react", "ui"),
            ["next"] = new("next", "full-stack"),
            ["vue"] = new("vue", "ui"),
            ["nuxt"] = new("nuxt", "full-stack"),
            ["svelte"] = new("svelte", "ui"),
            ["@sveltejs/kit"] = new("sveltekit", "full-stack"),
            ["@angular/core"] = new("angular", "ui"),
            ["solid-js"] = new("solid", "ui"),
            ["preact"] = new("preact", "ui"),
            ["express"] = new("express", "server"),
            ["@nestjs/core"] = new("nestjs", "server"),
            ["fastify"] = new("fastify", "server"),
            ["hono"] = new("hono", "server"),
            ["koa"] = new("koa", "server"),
            ["elysia"] = new("elysia", "server"),
            ["@apollo/server"] = new("apollo", "server"),
            ["graphql-yoga"] = new("graphql-yoga", "server"),
            ["prisma"] = new("prisma", "data"),
            ["@prisma/client"] = new("prisma", "data"),
            ["drizzle-orm"] = new("drizzle", "data"),
            ["typeorm"] = new("typeorm", "data"),
            ["sequelize"] = new("sequelize", "data"),
            ["mongoose"] = new("mongoose", "data"),
            ["knex"] = new("knex", "data"),
            ["kysely"] = new("kysely", "data"),
            ["pg"] = new("postgresql", "data"),
            ["mysql2"] = new("mysql", "data"),
            ["better-sqlite3"] = new("sqlite", "data"),
            ["axios"] = new("axios", "integration"),
            ["stripe"] = new("stripe", "integration"),
            ["twilio"] = new("twilio", "integration"),
            ["@sendgrid/mail"] = new("sendgrid", "integration"),
            ["firebase"] = new("firebase", "integration"),
            ["firebase-admin"] = new("firebase", "integration"),
            ["amqplib"] = new("rabbitmq", "integration"),
            ["kafkajs"] = new("kafka", "integration"),
            ["socket.io"] = new("socket.io", "integration"),
            ["graphql-request"] = new("graphql", "integration"),
            ["@grpc/grpc-js"] = new("grpc", "integration"),
            ["passport"] = new("passport", "security"),
            ["next-auth"] = new("authjs", "security"),
            ["@auth/core"] = new("authjs", "security"),
            ["jose"] = new("jose", "security"),
            ["jsonwebtoken"] = new("jwt", "security"),
            ["bcrypt"] = new("bcrypt", "security"),
            ["bcryptjs"] = new("bcrypt", "security"),
            ["argon2"] = new("argon2", "security"),
            ["helmet"] = new("helmet", "security"),
            ["zod"] = new("zod", "validation"),
            ["yup"] = new("yup", "validation"),
            ["joi"] = new("joi", "validation"),
            ["ajv"] = new("ajv", "validation"),
            ["valibot"] = new("valibot", "validation"),
            ["class-validator"] = new("class-validator", "validation"),
            ["bullmq"] = new("bullmq", "background"),
            ["bull"] = new("bull", "background"),
            ["agenda"] = new("agenda", "background"),
            ["node-cron"] = new("node-cron", "background"),
            ["bree"] = new("bree", "background"),
            ["vitest"] = new("vitest", "test-unit"),
            ["jest"] = new("jest", "test-unit"),
            ["mocha"] = new("mocha", "test-unit"),
            ["jasmine"] = new("jasmine", "test-unit"),
            ["ava"] = new("ava", "test-unit"),
            ["@playwright/test"] = new("playwright", "test-e2e"),
            ["cypress"] = new("cypress", "test-e2e"),
            ["puppeteer"] = new("puppeteer", "test-e2e"),
            ["webdriverio"] = new("webdriverio", "test-e2e"),
            ["supertest"] = new("supertest", "test-integration"),
            ["testcontainers"] = new("testcontainers", "test-integration"),
            ["@pact-foundation/pact"] = new("pact", "test-integration"),
            ["@testing-library/react"] = new("testing-library", "test-component"),
            ["@testing-library/vue"] = new("testing-library", "test-component"),
            ["@testing-library/svelte"] = new("testing-library", "test-component"),
            ["typescript"] = new("typescript", "build"),
            ["vite"] = new("vite", "build"),
            ["webpack"] = new("webpack", "build"),
            ["rollup"] = new("rollup", "build"),
            ["esbuild"] = new("esbuild", "build"),
            ["tsup"] = new("tsup", "build"),
            ["turbo"] = new("turborepo", "workspace"),
            ["nx"] = new("nx", "workspace"),
            ["lerna"] = new("lerna", "workspace"),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static TechnologyClassification? Classify(string packageName)
    {
        if (Packages.TryGetValue(packageName, out TechnologyClassification? classification))
        {
            return classification;
        }

        if (packageName.StartsWith("@aws-sdk/", StringComparison.OrdinalIgnoreCase))
        {
            return new TechnologyClassification("aws", "integration");
        }

        if (packageName.StartsWith("@azure/", StringComparison.OrdinalIgnoreCase))
        {
            return new TechnologyClassification("azure", "integration");
        }

        if (packageName.StartsWith("@nestjs/", StringComparison.OrdinalIgnoreCase))
        {
            return new TechnologyClassification("nestjs", "server");
        }

        return null;
    }
}

internal sealed record TechnologyClassification(string Name, string Family);
