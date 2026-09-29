using System.Text;
using System.Text.Json;
using DazPose.App.Models;
using DazPose.App.Services;
using DazPose.Core;

namespace DazPose.Tests;

public sealed class MorphPipelineTests
{
    private const string FuntasyUrl = "name://@selection#FUNtasy%20Face:?value/value";

    [Fact]
    public void ParsesStaticFigureControlAndPreservesRawAndDecodedIdentity()
    {
        var pose = ParseControl(FuntasyUrl, [new DazPoseKey(0, 1)]);
        var control = Assert.Single(pose.FigureControls);
        var channel = Assert.Single(pose.Channels);

        Assert.Equal(FuntasyUrl, control.SourceUrl);
        Assert.Equal("FUNtasy%20Face", control.RawControlId);
        Assert.Equal("FUNtasy Face", control.DecodedControlName);
        Assert.Equal(1f, control.Value);
        Assert.True(channel.IsSupportedFigureControlChannel);
        Assert.Empty(pose.UnsupportedChannels);
        PoseConversionService.ValidatePose(pose);
    }

    [Fact]
    public void ActiveSupportedFigureControlIsWrittenAsCanonicalVersionTwo()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var source = WriteShapePreset(Path.Combine(temp, "!!FUNtasy Face.duf"), FuntasyUrl, 1);
            var canonical = Path.Combine(temp, "converted", "FUNtasy.dazpose.json");
            var result = PoseConversionService.ConvertCanonical(FixtureData.FigurePath, source, canonical);

            Assert.Equal(0, result.Pose.SkeletalTargetCount);
            var json = JsonDocument.Parse(File.ReadAllText(canonical));
            using (json)
            {
                var root = json.RootElement;
                Assert.Equal(2, root.GetProperty("version").GetInt32());
                var control = Assert.Single(root.GetProperty("figureControls").EnumerateArray());
                Assert.Equal(FuntasyUrl, control.GetProperty("sourceUrl").GetString());
                Assert.Equal("FUNtasy%20Face", control.GetProperty("rawControlId").GetString());
                Assert.Equal("FUNtasy Face", control.GetProperty("name").GetString());
                Assert.Equal(1f, control.GetProperty("value").GetSingle());
            }
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Theory]
    [InlineData("name://@selection#FUNtasy%20Face:?value/foo", 1)]
    [InlineData("name://@selection#FUNtasy%20Face:?value/value", 2)]
    [InlineData("name://@selection#FUNtasy%20Face:?min/value", 1)]
    public void ActiveUnsupportedOrAnimatedControlStillFailsConversion(string url, int keyCount)
    {
        var keys = keyCount == 1
            ? new[] { new DazPoseKey(0, 1) }
            : new[] { new DazPoseKey(0, 0), new DazPoseKey(1, 1) };
        var pose = ParseControl(url, keys);

        Assert.Empty(pose.FigureControls);
        var error = Assert.Throws<DazConversionException>(() => PoseConversionService.ValidatePose(pose));
        Assert.Contains("Unsupported non-neutral channel", error.Message, StringComparison.Ordinal);
        Assert.Contains(url, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredMorphManifestDeduplicatesAndGeneratesExactFuntasyCsv()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var source = Path.Combine(temp, "People", "Genesis 8 Female", "Shapes", "FUNtasy", "!!FUNtasy Face.duf");
            var control = new DazFigureControlValue(FuntasyUrl, "FUNtasy%20Face", "FUNtasy Face", 1);
            var manifest = new RequiredMorphManifestService();

            Assert.Equal(1, manifest.AddCandidateControls(project, [control], source, Path.Combine(temp, "People")));
            Assert.Equal(0, manifest.AddCandidateControls(project, [control], source, Path.Combine(temp, "People")));
            var result = manifest.GenerateExportRules(project);

            Assert.Equal(1, result.ExportRuleCount);
            Assert.Equal(Path.Combine(project, ".dazposewizard", "DazPoseWizard-MorphExportRules.csv"), result.CsvPath);
            Assert.Equal("\"FUNtasy Face\",\"Export\"\r\n\"Anything\",\"Bake\"\r\n", File.ReadAllText(result.CsvPath));
            using var json = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
            var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("FUNtasy%20Face", item.GetProperty("rawControlId").GetString());
            Assert.Equal("FUNtasy Face", item.GetProperty("name").GetString());
            Assert.Equal("Candidate", item.GetProperty("state").GetString());
            Assert.Equal("Genesis 8 Female/Shapes/FUNtasy/!!FUNtasy Face.duf", item.GetProperty("firstSeenIn").GetString());
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void ExportRulesAreSortedAndEscapeCommasAndQuotes()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var service = new RequiredMorphManifestService();
            var controls = new[]
            {
                new DazFigureControlValue("url-a", "wide", "Smile, \"Wide\"", 1),
                new DazFigureControlValue("url-b", "fun", "FUNtasy Face", 1)
            };
            service.AddCandidateControls(project, controls, Path.Combine(temp, "pose.duf"));
            var result = service.GenerateExportRules(project);

            Assert.Equal(2, result.ExportRuleCount);
            Assert.Equal("\"FUNtasy Face\",\"Export\"\r\n\"Smile, \"\"Wide\"\"\",\"Export\"\r\n\"Anything\",\"Bake\"\r\n",
                File.ReadAllText(result.CsvPath));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void ExportRuleCsvIsExactUnionOfEnabledPinsAndContentRequirements()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var service = new RequiredMorphManifestService();
            service.SaveAlwaysExportEntries(project,
            [
                Pin("eCTRLvAA", "LipSync", "Viseme fallback"),
                Pin("PBMGraceYongBreastsSize", "BodyCustomization", "Character-specific fallback"),
                Pin("DisabledPin", "Manual", "Not exported", enabled: false)
            ]);
            service.AddCandidateControls(project,
            [
                new DazFigureControlValue("content-both", "body-size", "PBMGraceYongBreastsSize", 1),
                new DazFigureControlValue("content-only", "funtasy", "FUNtasy Face", 1)
            ], Path.Combine(temp, "pose.duf"));

            var result = service.GenerateExportRules(project);

            Assert.Equal(3, result.ExportRuleCount);
            Assert.Equal(2, result.AlwaysExportRuleCount);
            Assert.Equal(2, result.ContentRequiredRuleCount);
            Assert.Equal(
                "\"PBMGraceYongBreastsSize\",\"Export\"\r\n\"eCTRLvAA\",\"Export\"\r\n\"FUNtasy Face\",\"Export\"\r\n\"Anything\",\"Bake\"\r\n",
                File.ReadAllText(result.CsvPath));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void DisablingOrRemovingPinOnlyMorphRemovesItFromExportRules()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var service = new RequiredMorphManifestService();
            service.SaveAlwaysExportEntries(project, [Pin("eCTRLvAA", "LipSync", "Viseme")]);
            Assert.Equal(1, service.GenerateExportRules(project).ExportRuleCount);

            var disabled = Assert.Single(service.GetAlwaysExportEntries(project));
            disabled.AlwaysExport = false;
            service.SaveAlwaysExportEntries(project, [disabled]);
            Assert.Equal("\"Anything\",\"Bake\"\r\n", File.ReadAllText(service.GenerateExportRules(project).CsvPath));

            service.SaveAlwaysExportEntries(project, []);
            var removed = service.GenerateExportRules(project);
            Assert.Equal(0, removed.ExportRuleCount);
            Assert.Equal("\"Anything\",\"Bake\"\r\n", File.ReadAllText(removed.CsvPath));
            using var json = JsonDocument.Parse(File.ReadAllText(removed.ManifestPath));
            Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void DisabledOrRemovedPinRemainsExportedWhenConvertedContentRequiresIt()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var service = new RequiredMorphManifestService();
            service.SaveAlwaysExportEntries(project, [Pin("PBMGraceYongBreastsSize", "BodyCustomization", "Body control")]);
            service.AddCandidateControls(project,
                [new DazFigureControlValue("content", "size", "PBMGraceYongBreastsSize", 1)], Path.Combine(temp, "pose.duf"));

            var entry = Assert.Single(service.GetAlwaysExportEntries(project));
            entry.AlwaysExport = false;
            service.SaveAlwaysExportEntries(project, [entry]);
            var stillRequired = service.GenerateExportRules(project);
            Assert.Equal(1, stillRequired.ExportRuleCount);
            Assert.Equal(0, stillRequired.AlwaysExportRuleCount);
            Assert.Equal(1, stillRequired.ContentRequiredRuleCount);
            Assert.Equal("\"PBMGraceYongBreastsSize\",\"Export\"\r\n\"Anything\",\"Bake\"\r\n", File.ReadAllText(stillRequired.CsvPath));

            service.SaveAlwaysExportEntries(project, []);
            var contentOnly = service.GenerateExportRules(project);
            Assert.Equal(1, contentOnly.ExportRuleCount);
            Assert.Equal("\"PBMGraceYongBreastsSize\",\"Export\"\r\n\"Anything\",\"Bake\"\r\n", File.ReadAllText(contentOnly.CsvPath));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void AlwaysExportManifestRoundTripsCategoryPurposeAndContentProvenance()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var service = new RequiredMorphManifestService();
            service.SaveAlwaysExportEntries(project, [Pin("eCTRLvAA", "LipSync", "Standard viseme fallback")]);
            var pin = Assert.Single(service.GetAlwaysExportEntries(project));

            Assert.Equal("LipSync", pin.Category);
            Assert.Equal("Standard viseme fallback", pin.Purpose);
            Assert.False(pin.RequiredByContent);
            Assert.True(pin.AlwaysExport);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, ".dazposewizard", "required-morphs.json")));
            Assert.Equal(2, json.RootElement.GetProperty("schemaVersion").GetInt32());
            var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("LipSync", item.GetProperty("category").GetString());
            Assert.Equal("Standard viseme fallback", item.GetProperty("purpose").GetString());
            Assert.False(item.GetProperty("requiredByContent").GetBoolean());
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void ContentOnlyEntriesRemainVisibleAsProvenanceWithoutBecomingPins()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var service = new RequiredMorphManifestService();
            service.AddCandidateControls(project,
                [new DazFigureControlValue("content", "face", "FUNtasy Face", 1)], Path.Combine(temp, "pose.duf"));

            var entry = Assert.Single(service.GetAlwaysExportEntries(project));
            Assert.True(entry.RequiredByContent);
            Assert.False(entry.AlwaysExport);
            Assert.Null(entry.Category);
            service.SaveAlwaysExportEntries(project, [entry]);
            var rules = service.GenerateExportRules(project);

            Assert.Equal(0, rules.AlwaysExportRuleCount);
            Assert.Equal(1, rules.ContentRequiredRuleCount);
            Assert.Equal(1, rules.ExportRuleCount);
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void VersionOneManifestMigratesExistingEntriesToContentRequired()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var manifestDirectory = Path.Combine(project, ".dazposewizard");
            Directory.CreateDirectory(manifestDirectory);
            File.WriteAllText(Path.Combine(manifestDirectory, "required-morphs.json"),
                "{\"schemaVersion\":1,\"items\":[{\"rawControlId\":\"legacy\",\"name\":\"Legacy Morph\",\"state\":\"Candidate\",\"firstSeenIn\":\"pose.duf\"}]}");
            var service = new RequiredMorphManifestService();

            var result = service.GenerateExportRules(project);

            Assert.Equal(1, result.ContentRequiredRuleCount);
            Assert.Equal("\"Legacy Morph\",\"Export\"\r\n\"Anything\",\"Bake\"\r\n", File.ReadAllText(result.CsvPath));
            using var json = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
            Assert.Equal(2, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.True(Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()).GetProperty("requiredByContent").GetBoolean());
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public async Task ShapePresetConversionAddsActiveControlToProjectManifest()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var sourceRoot = Path.Combine(temp, "DAZ Library");
            var source = WriteShapePreset(Path.Combine(sourceRoot, "Shapes", "FUNtasy", "!!FUNtasy Face.duf"), FuntasyUrl, 1);
            var settings = new AppSettings
            {
                DazContentRoot = sourceRoot,
                FigureDefinitionPath = FixtureData.FigurePath,
                UnityProjectRoot = project,
                FinalPoseAssetRoot = "Assets/Animations/DazPoses",
                CanonicalImportRoot = "Assets/DazPoseImports"
            };
            var projectService = new UnityProjectService();
            var registry = new ConversionRegistryService(projectService);
            var naming = new PoseOutputNamingService(projectService);

            await using var queue = new ConversionQueueService(() => settings, projectService, registry, naming, concurrency: 1);
            await queue.EnqueueAsync([new ConversionRequest(source, "Shapes/FUNtasy")]);
            var firstJob = Assert.Single(queue.Jobs);
            await WaitForJobAsync(firstJob);
            Assert.Equal(ConversionJobState.AwaitingUnity, firstJob.State);
            using (var canonical = JsonDocument.Parse(File.ReadAllText(firstJob.CanonicalPath!)))
                Assert.Single(canonical.RootElement.GetProperty("figureControls").EnumerateArray());

            await queue.EnqueueAsync([new ConversionRequest(source, "Shapes/FUNtasy")]);
            var secondJob = queue.Jobs.First(job => !ReferenceEquals(job, firstJob));
            await WaitForJobAsync(secondJob);
            Assert.Equal(ConversionJobState.AwaitingUnity, secondJob.State);

            var manifestPath = Path.Combine(project, ".dazposewizard", "required-morphs.json");
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Single(manifest.RootElement.GetProperty("items").EnumerateArray());
            var rules = new RequiredMorphManifestService().GenerateExportRules(project);
            Assert.Equal(1, rules.ExportRuleCount);
            Assert.Equal("\"FUNtasy Face\",\"Export\"\r\n\"Anything\",\"Bake\"\r\n", File.ReadAllText(rules.CsvPath));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    private static DazPose.Core.DazPose ParseControl(string url, IReadOnlyList<DazPoseKey> keys)
    {
        var payload = JsonSerializer.Serialize(new
        {
            asset_info = new { id = "synthetic:control", type = "preset_shape", label = "Synthetic Shape" },
            scene = new
            {
                animations = new[]
                {
                    new { url, keys = keys.Select(key => new[] { key.Time, key.Value }).ToArray() }
                }
            }
        });
        using var document = JsonDocument.Parse(payload);
        return DazPoseParser.Parse("synthetic-shape.duf", document, FixtureData.LoadFigure());
    }

    private static string WriteShapePreset(string path, string url, float value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = JsonSerializer.Serialize(new
        {
            asset_info = new { id = "shape:funtasy-face", type = "preset_shape", label = "FUNtasy Face" },
            scene = new { animations = new[] { new { url, keys = new[] { new[] { 0f, value } } } } }
        });
        File.WriteAllText(path, payload, new UTF8Encoding(false));
        return path;
    }

    private static string CreateUnityProject(string temp)
    {
        var root = Path.Combine(temp, "UnityProject");
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        return root;
    }

    private static RequiredMorphManifestItem Pin(string name, string category, string purpose, bool enabled = true) => new()
    {
        Name = name,
        Category = category,
        Purpose = purpose,
        AlwaysExport = enabled
    };

    private static async Task WaitForJobAsync(ConversionJob job)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (job.State is ConversionJobState.Queued or ConversionJobState.Converting && DateTime.UtcNow < deadline)
            await Task.Delay(25);
        Assert.NotEqual(ConversionJobState.Queued, job.State);
        Assert.NotEqual(ConversionJobState.Converting, job.State);
    }
}
