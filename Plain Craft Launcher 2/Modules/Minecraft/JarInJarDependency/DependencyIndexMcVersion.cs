using CompFile = PCL.LocalComp.LocalCompFile;

namespace PCL;

public partial class JarInJarIndex
{
    private static List<CompFile> _CollectLoadableNodes(
        List<CompFile> embedded,
        string mc,
        IReadOnlyDictionary<string, string>? forgeSelections = null,
        IReadOnlyDictionary<string, string>? fabricSelections = null)
    {
        var nodes = new List<CompFile>();
        _CollectLoadableNodesRecursive(embedded, mc, nodes, forgeSelections, fabricSelections);
        return nodes;
    }

    private static void _CollectLoadableNodesRecursive(
        List<CompFile>? embedded,
        string mc,
        List<CompFile> nodes,
        IReadOnlyDictionary<string, string>? forgeSelections,
        IReadOnlyDictionary<string, string>? fabricSelections)
    {
        if (embedded is null)
            return;

        foreach (
            var node in
            from node in embedded
            where _NodeLoads(node, mc)
            where _ForgeNodeSelected(node, forgeSelections)
            where _FabricNodeSelected(node, fabricSelections)
            select node)
        {
            nodes.Add(node);
            _CollectLoadableNodesRecursive(
                node.EmbeddedMods,
                mc,
                nodes,
                forgeSelections,
                fabricSelections);
        }
    }

    private static bool _UsesFabricLoader(string? loader)
    {
        return string.Equals(loader, "Fabric", StringComparison.OrdinalIgnoreCase)
            || string.Equals(loader, "Quilt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _FabricNodeSelected(
        CompFile node,
        IReadOnlyDictionary<string, string>? selections)
    {
        if (selections is null
            || string.IsNullOrWhiteSpace(node.ModId)
            || !_UsesFabricLoader(node.JijLoader))
            return true;

        if (!selections.TryGetValue(node.ModId, out var selected))
            return false;

        return McConstraintMatcher.CompareFabricVersions(node.Version ?? "", selected) == 0;
    }

    private Dictionary<string, string> _SelectFabricVersions(
        IReadOnlyDictionary<CompFile, List<CompFile>> nodesByHost,
        string mc,
        IReadOnlyDictionary<string, string> forgeSelections)
    {
        var candidates = nodesByHost.Values.SelectMany(nodes => nodes).ToList();
        var baseRequirements = _BuildBaseFabricRequirements();
        var selections = _ChooseFabricVersions(candidates, baseRequirements);
        var seenSelectionKeys = new HashSet<string>(StringComparer.Ordinal);
        var maxPasses = candidates.Count + 1;

        for (var pass = 0; pass < maxPasses; pass++)
        {
            if (!seenSelectionKeys.Add(_FabricSelectionKey(selections)))
            {
                Common.Log(
                    "[Mod] Fabric 内嵌版本选择出现循环，保留当前候选并交由依赖检查报告冲突",
                    Common.LogLevel.Developer);
                return selections;
            }

            var requirements = _BuildFabricRequirements(
                nodesByHost,
                mc,
                forgeSelections,
                selections,
                baseRequirements);

            var nextSelections = _ChooseFabricVersions(candidates, requirements);
            if (_SameFabricSelections(selections, nextSelections))
                return nextSelections;

            selections = nextSelections;
        }

        Common.Log(
            "[Mod] Fabric 内嵌版本选择未在限定轮次内收敛，保留当前候选并交由依赖检查报告冲突",
            Common.LogLevel.Developer);
        return selections;
    }

    private List<DepRow> _BuildBaseFabricRequirements()
    {
        return
        [
            ..
            from mod in _allMods
                .Where(mod => mod.State == CompFile.LocalFileStatus.Fine)
            from declaration in mod.DependencyDeclarations
                .Where(declaration => !declaration.Optional)
            select new DepRow
            {
                DepId = declaration.Id,
                Raw = declaration.Raw,
                Loader = mod.DetectedLoader
            }
        ];
    }

    private static List<DepRow> _BuildFabricRequirements(
        IReadOnlyDictionary<CompFile, List<CompFile>> nodesByHost,
        string mc,
        IReadOnlyDictionary<string, string> forgeSelections,
        IReadOnlyDictionary<string, string> fabricSelections,
        List<DepRow> baseRequirements)
    {
        var requirements = new List<DepRow>(baseRequirements);
        requirements.AddRange(
            from host in nodesByHost.Keys
            from node in _CollectLoadableNodes(
                host.EmbeddedMods,
                mc,
                forgeSelections,
                fabricSelections)
            from declaration in node.DependencyDeclarations
                .Where(declaration => !declaration.Optional)
            select new DepRow
            {
                DepId = declaration.Id,
                Raw = declaration.Raw,
                Loader = node.JijLoader
            });

        return requirements;
    }

    private static Dictionary<string, string> _ChooseFabricVersions(
        List<CompFile> candidates,
        List<DepRow> requirements)
    {
        var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var groups = candidates
            .Where(node =>
                !string.IsNullOrWhiteSpace(node.ModId) && _UsesFabricLoader(node.JijLoader))
            .GroupBy(node => node.ModId!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var versions = group
                .Select(node => node.Version ?? "")
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            versions.Sort((left, right) =>
                McConstraintMatcher.CompareFabricVersions(right, left));

            var modRequirements = requirements
                .Where(requirement => string.Equals(
                    requirement.DepId,
                    group.Key,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            selections[group.Key] = versions.FirstOrDefault(candidateVersion =>
                modRequirements.All(requirement =>
                    _VersionSatisfies(requirement, candidateVersion))) ?? "";
        }

        return selections;
    }

    private static bool _SameFabricSelections(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(pair =>
                right.TryGetValue(pair.Key, out var value)
                && string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string _FabricSelectionKey(IReadOnlyDictionary<string, string> selections)
    {
        return string.Join(
            "\n",
            selections
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key + "\0" + pair.Value));
    }

    private static bool _ForgeNodeSelected(
        CompFile node,
        IReadOnlyDictionary<string, string>? selections)
    {
        if (selections is null || string.IsNullOrWhiteSpace(node.JijIdentifier))
            return true;

        if (!selections.TryGetValue(node.JijIdentifier, out var selected))
            return false;

        var version = node.JijArtifactVersion ?? node.Version;
        return !string.IsNullOrWhiteSpace(version)
            && McConstraintMatcher.CompareMavenVersions(version, selected) == 0;
    }

    private static Dictionary<string, string> _SelectForgeJarJarVersions(
        IEnumerable<CompFile> nodes)
    {
        var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var groups = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.JijIdentifier))
            .GroupBy(node => node.JijIdentifier!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var candidateVersions = group
                .Select(node => node.JijArtifactVersion ?? node.Version ?? "")
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            candidateVersions.Sort((left, right) =>
                McConstraintMatcher.CompareMavenVersions(right, left));

            selections[group.Key] = candidateVersions.FirstOrDefault(candidateVersion =>
                group.All(node =>
                    string.IsNullOrWhiteSpace(node.JijVersionRange)
                    || _JarJarRangeContains(node.JijVersionRange, candidateVersion))) ?? "";
        }

        return selections;
    }

    private static bool _JarJarRangeContains(string range, string candidate)
    {
        var trimmedRange = range.Trim();

        return trimmedRange.Length > 0 && trimmedRange[0] is not '[' and not '('
            ? McConstraintMatcher.CompareMavenVersions(candidate, trimmedRange) == 0
            : McConstraintMatcher.Satisfies(trimmedRange, "Forge", candidate);
    }

    private static bool _NodeLoads(CompFile node, string mc)
    {
        if (string.IsNullOrEmpty(mc))
            return true;

        var minecraftDependencies = node
            .DependencyDeclarations
            .Where(declaration =>
                string.Equals(declaration.Id, "minecraft", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var requiredDependencies = minecraftDependencies
            .Where(declaration => !declaration.Optional)
            .ToList();

        if (requiredDependencies.Count > 0)
        {
            var constrainedDependencies = requiredDependencies
                .Where(declaration => !string.IsNullOrWhiteSpace(declaration.Raw))
                .ToList();

            if (constrainedDependencies.Count == 0
                || constrainedDependencies.All(declaration =>
                    McConstraintMatcher.Satisfies(
                        declaration.Raw ?? "",
                        node.JijLoader ?? "",
                        mc)))
                return true;

            return _ContainsMinecraftVersionToken(node, mc);
        }

        if (minecraftDependencies.Count > 0)
            return true;

        var targetVersion = node.JijTargetMcVersion;
        if (string.IsNullOrWhiteSpace(targetVersion))
            return true;

        if (McConstraintMatcher.Satisfies(targetVersion, node.JijLoader ?? "", mc))
            return true;

        return _ContainsMinecraftVersionToken(node, mc);
    }

    private static bool _ContainsMinecraftVersionToken(CompFile node, string mc)
    {
        return McConstraintMatcher.ContainsVersionToken(node.FileName, mc)
            || McConstraintMatcher.ContainsVersionToken(node.Version ?? "", mc);
    }
}
