using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class JavaScriptTestRecognitionTests
{
    [Theory]
    [InlineData("js")]
    [InlineData("jsx")]
    [InlineData("ts")]
    [InlineData("tsx")]
    public async Task ProductionCallsDoNotCreateTests(string extension)
    {
        InMemoryRepository repository = new();
        repository.WriteText("package.json", """{"devDependencies":{"vitest":"1.0.0"}}""");
        repository.WriteText("src/validation." + extension, """
            const regex = /^[-]?\d+$/;
            export function validate(value, checker) {
                checker.test(value); checker.it(value); checker.describe(value);
                return regex.test(value) && /ready/.test(value);
            }
            """);
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.JavaScriptTest);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
        EstimateReport report = new SeedEstimator().Estimate(evidence, EstimationProfile.Recreation);
        Assert.DoesNotContain(report.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Contains(report.ProfessionalizationGap, item => item.Category == EffortCategory.UnitTesting);
    }

    [Theory]
    [InlineData("js", "import { test as check } from 'vitest'; check('works', () => {});", 1)]
    [InlineData("ts", "import { test as check } from 'vitest'; check('works', () => {});", 1)]
    [InlineData("jsx", "import * as runner from '@jest/globals'; runner.test('works', () => {});", 1)]
    [InlineData("tsx", "import * as runner from 'vitest'; runner.test('works', () => {});", 1)]
    [InlineData("js", "import { it } from 'mocha'; it.only('works', () => {});", 1)]
    [InlineData("ts", "import { test } from 'vitest'; test.each([1,2])('works', () => {});", 1)]
    [InlineData("js", "import { test } from 'vitest'; test.each([1,2])('works', () => {});", 1)]
    [InlineData("js", "import { test } from 'vitest'; test.each`a | b`('works', () => {});", 1)]
    [InlineData("ts", "import { test } from 'vitest'; test.each`a | b`('works', () => {});", 1)]
    [InlineData("js", "import test from 'node:test'; test('works', () => {});", 1)]
    [InlineData("js", "import { test } from '@playwright/test'; test.describe('suite', () => { test('works', () => {}); });", 1)]
    [InlineData("js", "const { it: check } = require('mocha'); check('works', () => {});", 1)]
    [InlineData("js", "const runner = require('vitest'); runner.test('works', () => {});", 1)]
    public async Task ImportedFrameworkBindingsAndModifiersAreRecognized(string extension, string source, int expected)
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/checks." + extension, source);
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
        Assert.Equal(expected, Cases(evidence));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.RepositoryEvidence, ContractJson.Serialize(evidence)).IsValid);
    }

    [Theory]
    [InlineData("js")]
    [InlineData("ts")]
    public async Task TestFileGlobalsRemainSupported(string extension)
    {
        InMemoryRepository repository = new();
        repository.WriteText("tests/example.test." + extension,
            "describe('suite', () => { it('works', () => { expect(true).toBe(true); }); });");
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
        Assert.Equal(1, Cases(evidence));
        EvidenceFact fact = Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.JavaScriptTest);
        Assert.Equal(1, fact.Measurements.Single(value => value.Name == "test-suites").Value);
        Assert.Equal(1, fact.Measurements.Single(value => value.Name == "assertions").Value);
    }

    [Theory]
    [InlineData("import { test } from 'vitest'; function validate(test) { test('production'); } test('real', () => {});", 1)]
    [InlineData("import { test } from 'vitest'; { const test = value => value; test('production'); } test('real', () => {});", 1)]
    [InlineData("const test = value => value; test('production');", 0)]
    [InlineData("function test(value) { return value; } test('production');", 0)]
    [InlineData("import { test } from './production.js'; test('production');", 0)]
    [InlineData("import { test } from 'vitest'; test = custom; test('production');", 0)]
    [InlineData("function run(require) { const runner = require('vitest'); runner.test('production'); }", 0)]
    [InlineData("const run = class test { validate() { test('production'); } };", 0)]
    [InlineData("let { test } = require('vitest'); ({ test } = production); test('production');", 0)]
    public async Task ParserBackedShadowingDoesNotInventTestCases(string source, int expected)
    {
        InMemoryRepository repository = new();
        repository.WriteText("tests/example.test.js", source);
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
        Assert.Equal(expected, Cases(evidence));
    }

    [Theory]
    [InlineData("import { test } from 'vitest'; function validate(test: Function) { test('production'); }")]
    [InlineData("import { test } from 'vitest'; function validate(test: Function): boolean { test('production'); return true; }")]
    [InlineData("import { test } from 'vitest'; const validate = (test: Function): boolean => { test('production'); return true; };")]
    [InlineData("import type { test } from 'vitest'; test('not a runtime import');")]
    [InlineData("import { type test } from 'vitest'; test('not a runtime import');")]
    [InlineData("import test from 'vitest'; test('not a supported default export');")]
    public async Task TypeScriptAmbiguousBindingsAreConservativelyUncounted(string source)
    {
        InMemoryRepository repository = new();
        repository.WriteText("src/checks.ts", source);
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
        Assert.Equal(0, Cases(evidence));
    }

    private static decimal Cases(RepositoryEvidence evidence) => evidence.Facts
        .Where(fact => fact.Kind == EvidenceKinds.JavaScriptTest)
        .SelectMany(fact => fact.Measurements).Where(value => value.Name == "test-cases").Sum(value => value.Value);
}
