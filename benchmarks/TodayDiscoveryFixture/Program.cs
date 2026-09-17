// Synthetic provider for Measure-TodayDiscovery.py. No network access.
using System.Globalization;
using System.Text.Json.Nodes;

JsonNode fixture = JsonNode.Parse(File.ReadAllText(
    Environment.GetEnvironmentVariable("EH_BENCHMARK_FIXTURE")!))!;
Thread.Sleep(fixture["delayMilliseconds"]!.GetValue<int>());
string endpoint = args[^1];
JsonNode response;
if (endpoint == "user")
{
    response = new JsonObject { ["login"] = "selected" };
}
else if (endpoint.StartsWith("user/emails", StringComparison.Ordinal))
{
    response = JsonNode.Parse("""[[{"email":"selected@example.test","verified":true}]]""")!;
}
else if (endpoint == "users/benchmark-owner")
{
    response = new JsonObject { ["type"] = "Organization" };
}
else if (endpoint.StartsWith("orgs/benchmark-owner/repos", StringComparison.Ordinal))
{
    JsonArray repositories = [];
    for (int index = 0; index < 178; index++)
    {
        repositories.Add(new JsonObject
        {
            ["id"] = 1000 + index, ["full_name"] = $"benchmark-owner/repository-{index}",
            ["default_branch"] = "main",
        });
    }

    response = new JsonArray(new JsonArray(repositories.Take(100).Select(item => item!.DeepClone()).ToArray()),
        new JsonArray(repositories.Skip(100).Select(item => item!.DeepClone()).ToArray()));
}
else if (args.Contains("graphql") && args.Contains("--paginate"))
{
    response = JsonNode.Parse(
        """[{"data":{"user":{"pullRequests":{"totalCount":0,"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}]""")!;
}
else if (args.Contains("graphql"))
{
    JsonObject data = new();
    string[] names = args.Where(value => value.StartsWith("name", StringComparison.Ordinal)).ToArray();
    for (int index = 0; index < names.Length; index++)
    {
        int repository = int.Parse(names[index].Split("repository-")[1], CultureInfo.InvariantCulture);
        JsonArray commits = RestCommits(repository);
        JsonArray graph = [];
        foreach (JsonNode commit in commits.Take(100))
        {
            JsonNode metadata = commit["commit"]!;
            JsonNode author = metadata["author"]!.DeepClone();
            author["user"] = commit["author"]!.DeepClone();
            graph.Add(new JsonObject
            {
                ["oid"] = commit["sha"]!.DeepClone(),
                ["parents"] = new JsonObject { ["nodes"] = new JsonArray(commit["parents"]!.AsArray()
                    .Select(parent => (JsonNode)new JsonObject { ["oid"] = parent!["sha"]!.DeepClone() }).ToArray()) },
                ["author"] = author, ["committer"] = metadata["committer"]!.DeepClone(),
                ["authoredDate"] = metadata["author"]!["date"]!.DeepClone(),
                ["committedDate"] = metadata["committer"]!["date"]!.DeepClone(),
                ["message"] = metadata["message"]!.DeepClone(),
            });
        }

        data["r" + index] = new JsonObject { ["defaultBranchRef"] = new JsonObject
        {
            ["name"] = "main", ["target"] = new JsonObject { ["history"] = new JsonObject
            {
                ["nodes"] = graph, ["pageInfo"] = new JsonObject { ["hasNextPage"] = repository == 177 },
            } },
        } };
    }

    response = new JsonObject { ["data"] = data };
}
else if (endpoint.StartsWith("repos/benchmark-owner/repository-", StringComparison.Ordinal) &&
    endpoint.Contains("/commits?", StringComparison.Ordinal))
{
    int repository = int.Parse(endpoint.Split("repository-")[1].Split('/')[0], CultureInfo.InvariantCulture);
    JsonArray commits = RestCommits(repository);
    response = new JsonArray(new JsonArray(commits.Take(100).Select(item => item!.DeepClone()).ToArray()));
    if (commits.Count > 100)
    {
        response.AsArray().Add(new JsonArray(commits.Skip(100).Select(item => item!.DeepClone()).ToArray()));
    }
}
else if (endpoint.Contains("/pulls?state=open", StringComparison.Ordinal))
{
    response = new JsonArray(new JsonArray());
}
else
{
    Console.Error.WriteLine("Unexpected synthetic provider request.");
    return 1;
}

Console.WriteLine(response.ToJsonString());
return 0;

JsonArray RestCommits(int repository)
{
    if (repository == 0)
    {
        return fixture["commits"]!.DeepClone().AsArray();
    }

    if (repository != 177)
    {
        return [];
    }

    JsonArray commits = [];
    for (int index = 1; index <= 101; index++)
    {
        JsonNode commit = fixture["commits"]![0]!.DeepClone();
        commit["sha"] = index.ToString("x40", CultureInfo.InvariantCulture);
        commit["author"]!["login"] = "unselected";
        commit["commit"]!["author"]!["name"] = "Unselected Person";
        commit["commit"]!["author"]!["email"] = "unselected@example.test";
        commits.Add(commit);
    }

    return commits;
}
