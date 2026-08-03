using Dapper;
using Microsoft.Data.SqlClient;

namespace PriceAggregator.Core.Categorization;

// One-off cleanup for Category rows that were created as separate nodes before
// CategoryResolver started reconciling near-duplicate names (see CategoryResolver.FindFuzzyMatchAsync).
// Walks the tree top-down (roots first) so parents are merged before their children are
// compared — two children only count as duplicates once they sit under the same surviving parent.
public static class CategoryMerger
{
    public static async Task<int> MergeDuplicatesAsync(SqlConnection db)
    {
        int totalMerged = 0;
        var levelParentIds = new List<int?> { null };

        while (levelParentIds.Count > 0)
        {
            var nextLevelParentIds = new List<int?>();

            foreach (var parentId in levelParentIds)
            {
                var siblings = (await db.QueryAsync<(int Id, string Name)>(@"
                    SELECT Id, Name FROM Categories
                    WHERE (ParentCategoryId = @ParentId) OR (@ParentId IS NULL AND ParentCategoryId IS NULL)",
                    new { ParentId = parentId })).ToList();

                // Group siblings into connected components under NamesMatch rather than
                // greedily consuming pairs in one pass: "Süt, Kahvaltılık" / "Süt Ürünleri &
                // Kahvaltılık" / "sut-urunleri" don't all match each other pairwise, but the
                // middle name bridges the other two, so all three must end up in one group.
                int n = siblings.Count;
                var parent = Enumerable.Range(0, n).ToArray();

                int Find(int x)
                {
                    while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                    return x;
                }
                void Union(int a, int b)
                {
                    var ra = Find(a);
                    var rb = Find(b);
                    if (ra != rb) parent[ra] = rb;
                }

                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        if (FuzzyMatcher.NamesMatch(siblings[i].Name, siblings[j].Name))
                            Union(i, j);

                var byRoot = new Dictionary<int, List<(int Id, string Name)>>();
                for (int i = 0; i < n; i++)
                {
                    var root = Find(i);
                    if (!byRoot.TryGetValue(root, out var list))
                        byRoot[root] = list = new List<(int, string)>();
                    list.Add(siblings[i]);
                }

                foreach (var group in byRoot.Values)
                {
                    if (group.Count < 2)
                    {
                        nextLevelParentIds.Add(group[0].Id);
                        continue;
                    }

                    var keep = PickCanonical(group);
                    foreach (var duplicate in group.Where(m => m.Id != keep.Id))
                    {
                        await MergeIntoAsync(db, keepId: keep.Id, duplicateId: duplicate.Id);
                        totalMerged++;
                    }

                    nextLevelParentIds.Add(keep.Id);
                }
            }

            levelParentIds = nextLevelParentIds.Select(id => (int?)id).ToList();
        }

        return totalMerged;
    }

    // Prefers a name that looks like a real display name (has at least one uppercase
    // letter) over one that looks like a raw classifier slug (e.g. "sut-urunleri" left
    // over from before CategoryClassifier gained proper display names); falls back to the
    // lowest Id (oldest / most likely to already be referenced elsewhere) among ties.
    private static (int Id, string Name) PickCanonical(List<(int Id, string Name)> group)
    {
        var properLooking = group.Where(m => m.Name.Any(char.IsUpper)).ToList();
        var pool = properLooking.Count > 0 ? properLooking : group;
        return pool.OrderBy(m => m.Id).First();
    }

    private static async Task MergeIntoAsync(SqlConnection db, int keepId, int duplicateId)
    {
        await db.ExecuteAsync(
            "UPDATE Products SET CategoryId = @KeepId WHERE CategoryId = @DuplicateId",
            new { KeepId = keepId, DuplicateId = duplicateId });

        await db.ExecuteAsync(
            "UPDATE Categories SET ParentCategoryId = @KeepId WHERE ParentCategoryId = @DuplicateId",
            new { KeepId = keepId, DuplicateId = duplicateId });

        await db.ExecuteAsync(
            "DELETE FROM Categories WHERE Id = @DuplicateId",
            new { DuplicateId = duplicateId });
    }
}
