using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioReconcilerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactRetainedRewritesRequireDisjointHeadReachability(bool sharedHead)
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState final = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"));
        ChangeEstimateReport source = await ReportAsync(ChangeSelectionKind.Commit, 0, initial, final);
        ChangePortfolioCandidate first = Candidate("repo", "first", source) with
        {
            Attribution = Candidate("repo", "first", source).Attribution with { HeadIds = ["default"] },
        };
        ChangePortfolioCandidate rewrite = Candidate("repo", "rewrite", source) with
        {
            Attribution = first.Attribution with { HeadIds = [sharedHead ? "default" : "retained"] },
        };
        ChangePortfolioItemDraft[] drafts = [ChangePortfolioIdentity.CreateDraft(first), ChangePortfolioIdentity.CreateDraft(rewrite)];
        ChangePortfolioExactCompositionNormalizer.Mark(drafts);
        Assert.Equal(sharedHead ? 0 : 1, drafts.Count(draft => draft.DuplicateOfItemId is not null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredPairKeepsItsDatePolicyWhenAnEarlierUnpairedRepresentationRepeatsIt(bool sharedHead)
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState final = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"));
        ChangeEstimateReport source = await ReportAsync(ChangeSelectionKind.Commit, 0, initial, final);
        DateTimeOffset date = new(2026, 1, 19, 12, 0, 0, TimeSpan.Zero);
        ChangePortfolioCandidate declared = Candidate("repo", "declared", source, date.AddDays(3));
        declared = declared with
        {
            Attribution = declared.Attribution with
            {
                HeadIds = ["paired"],
                Rewrite = new()
                {
                    Role = "rewritten",
                    SupportOnly = false,
                    OriginalAuthorTimestamp = date,
                    RewrittenCommitterTimestamp = date.AddDays(3),
                    Evidence = new()
                    {
                        OriginalObjectId = new string('a', 40),
                        RewrittenObjectId = new string('b', 40),
                        OldBaseObjectId = new string('c', 40),
                        NewBaseObjectId = new string('d', 40),
                        EventTimestamp = date.AddDays(3)
                    }
                },
            }
        };
        ChangePortfolioCandidate copy = Candidate("repo", "earlier-copy", source, date.AddDays(-1));
        copy = copy with { Attribution = copy.Attribution with { HeadIds = [sharedHead ? "paired" : "copy"] } };
        ChangePortfolioItemDraft declaredDraft = ChangePortfolioIdentity.CreateDraft(declared), copyDraft = ChangePortfolioIdentity.CreateDraft(copy);
        ChangePortfolioExactCompositionNormalizer.Mark([copyDraft, declaredDraft]);
        Assert.False(declaredDraft.Suppressed);
        Assert.Equal(sharedHead ? null : declaredDraft.Id, copyDraft.DuplicateOfItemId);
    }

    [Fact]
    public async Task SquashCompositionKeepsRetainedDatesAndDoesNotMultiplyEffort()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState first = State(("Demo.csproj", ProjectFile),
            ("A.cs", "public class A { public int Value => 1; }"));
        ChangeState final = State(("Demo.csproj", ProjectFile),
            ("A.cs", "public class A { public int Value => 2; }"),
            ("B.cs", "public class B { public string Value => \"done\"; }"));
        ChangeEstimateReport a = await ReportAsync(ChangeSelectionKind.Commit, 0, initial, first);
        ChangeEstimateReport b = await ReportAsync(ChangeSelectionKind.Commit, 0, first, final);
        ChangeEstimateReport squash = await ReportAsync(ChangeSelectionKind.Commit, 0, initial, final);
        DateTimeOffset day = new(2026, 1, 19, 12, 0, 0, TimeSpan.Zero);
        ChangePortfolioCandidate[] retained =
        [Candidate("repo", "original-a", a, day), Candidate("repo", "original-b", b, day.AddDays(1))];
        ChangePortfolioSelection selection = AuthorPeriod(final.ObjectId);
        ChangePortfolioReport baseline = Reconcile(selection, retained);
        ChangePortfolioCandidate[] combined = [.. retained, Candidate("repo", "squash", squash, day.AddMonths(2))];
        ChangePortfolioReport report = Reconcile(selection, combined);

        Assert.Equal(baseline.TotalEffort, report.TotalEffort);
        ChangePortfolioItemEstimate duplicate = Assert.Single(report.Items, item => item.ExactComposition is not null);
        Assert.Equal("squash", duplicate.SelectorId);
        Assert.Equal(0m, duplicate.AllocatedExpectedHours);
        Assert.Equal(2, duplicate.ExactComposition!.ItemIds.Count);
        Assert.Equal(duplicate.PatchDigest, duplicate.ExactComposition.PatchDigest);
        Assert.Contains(report.Adjustments, adjustment => adjustment.Kind == ChangePortfolioAdjustmentKind.ExactDuplicate);
        Assert.Equal(report.TotalEffort.Expected, report.Items.Sum(item => item.AllocatedExpectedHours));
        Assert.Equal(ContractJson.SerializeCompact(report),
            ContractJson.SerializeCompact(Reconcile(selection, [.. combined.Reverse()])));
        Assert.Empty(ContractValidation.Validate(report));
        SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioReport,
            ContractJson.Serialize(report));
        Assert.True(schema.IsValid, string.Join('\n', schema.Errors));
        ChangePortfolioReport invalid = report with
        {
            Items = [.. report.Items.Select(item => item.Id == duplicate.Id ? item with
            {
                ExactComposition = item.ExactComposition! with { ItemIds = [item.Id, item.Id] },
            } : item)],
        };
        Assert.NotEmpty(ContractValidation.Validate(invalid));
    }

    [Fact]
    public async Task SquashWithNewFollowUpIsNotDiscardedAsEquivalent()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState first = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"));
        ChangeState final = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"),
            ("B.cs", "public class B { }"));
        ChangeState extra = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"),
            ("B.cs", "public class B { }"), ("C.cs", "public class C { public int Read() => 7; }"));
        DateTimeOffset day = new(2026, 1, 19, 12, 0, 0, TimeSpan.Zero);
        ChangePortfolioReport report = Reconcile(AuthorPeriod(extra.ObjectId),
        [
            Candidate("repo", "a", await ReportAsync(ChangeSelectionKind.Commit, 0, initial, first), day),
            Candidate("repo", "b", await ReportAsync(ChangeSelectionKind.Commit, 0, first, final), day.AddDays(1)),
            Candidate("repo", "squash-extra", await ReportAsync(ChangeSelectionKind.Commit, 0, initial, extra), day.AddMonths(2)),
        ]);

        Assert.All(report.Items, item => Assert.Null(item.ExactComposition));
        Assert.True(report.Items.Single(item => item.SelectorId == "squash-extra").AllocatedExpectedHours > 0m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompositionRequiresDisjointReachabilityEvenWhenRevertIsNotSelected(bool sharedHead)
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState first = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"));
        ChangeState final = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"),
            ("B.cs", "public class B { }"));
        DateTimeOffset day = new(2026, 1, 19, 12, 0, 0, TimeSpan.Zero);
        ChangePortfolioCandidate[] candidates =
        [
            Candidate("repo", "first", await ReportAsync(ChangeSelectionKind.Commit, 0, initial, first), day),
            Candidate("repo", "second", await ReportAsync(ChangeSelectionKind.Commit, 0, first, final), day.AddDays(1)),
            Candidate("repo", "reintroduced", await ReportAsync(ChangeSelectionKind.Commit, 0, initial, final), day.AddDays(3)),
        ];
        ChangePortfolioItemDraft[] drafts = [.. candidates.Select((candidate, index) =>
            ChangePortfolioIdentity.CreateDraft(candidate with
            {
                Attribution = candidate.Attribution with
                {
                    HeadIds = [index == 2 && !sharedHead ? "rewritten" : "default"],
                },
            }))];

        ChangePortfolioExactCompositionNormalizer.Mark(drafts);

        Assert.Equal(sharedHead ? 0 : 1, drafts.Count(draft => draft.ExactComposition is not null));
        if (sharedHead)
        {
            Assert.All(drafts, draft => Assert.False(draft.Suppressed));
        }
    }

    [Fact]
    public async Task DisconnectedLookalikeDeltasAreNotACompositionProof()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState a = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"));
        ChangeState b = State(("Demo.csproj", ProjectFile), ("B.cs", "public class B { }"));
        ChangeState both = State(("Demo.csproj", ProjectFile), ("A.cs", "public class A { }"),
            ("B.cs", "public class B { }"));
        DateTimeOffset day = new(2026, 1, 19, 12, 0, 0, TimeSpan.Zero);
        ChangePortfolioReport report = Reconcile(AuthorPeriod(both.ObjectId),
        [
            Candidate("repo", "a", await ReportAsync(ChangeSelectionKind.Commit, 0, initial, a), day),
            Candidate("repo", "b", await ReportAsync(ChangeSelectionKind.Commit, 0, initial, b), day.AddDays(1)),
            Candidate("repo", "combined", await ReportAsync(ChangeSelectionKind.Commit, 0, initial, both), day.AddMonths(2)),
        ]);

        Assert.All(report.Items, item => Assert.Null(item.ExactComposition));
    }
}
