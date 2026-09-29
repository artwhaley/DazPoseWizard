using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DazPose.App.Models;
using DazPose.App.Services;
using DazPose.Core;

namespace DazPose.Tests;

public sealed class LibraryBrowserPipelineTests
{
    [Fact]
    public async Task IndexesPlainAndGzipPosePresetsAndFindsCompanionPreview()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var root = Path.Combine(temp, "DAZ Library");
            var product = Path.Combine(root, "People", "Genesis 8 Female", "Poses", "Product A");
            var nested = Path.Combine(product, "Subcategory");
            Directory.CreateDirectory(nested);
            var plainPath = Path.Combine(product, "Pose One.duf");
            var gzipPath = Path.Combine(nested, "Pose Two.duf");
            var rootPosePath = Path.Combine(root, "Root Pose.duf");
            var shapePath = Path.Combine(root, "People", "Genesis 8 Female", "Shapes", "FUNtasy", "!!FUNtasy Face.duf");
            WritePoseMetadata(plainPath, "pose:one", "Pose One", compressed: false);
            WritePoseMetadata(gzipPath, "pose:two", "Pose Two", compressed: true);
            WritePoseMetadata(rootPosePath, "pose:root", "Root Pose", compressed: false);
            WritePoseMetadata(shapePath, "shape:funtasy-face", "FUNtasy Face", "preset_shape", compressed: true);
            var previewPath = Path.ChangeExtension(plainPath, ".png");
            File.WriteAllBytes(previewPath, [1, 2, 3]);
            WritePoseMetadata(Path.Combine(product, "Scene.duf"), "scene:one", "Scene", "scene", compressed: false);

            var index = new LibraryIndexService(Path.Combine(temp, "cache", "index.db"));
            var result = await index.ScanAsync(root);
            var entries = await index.GetAllAsync(root);

            Assert.Equal(5, result.Visited);
            Assert.Equal(4, entries.Count);
            var shape = Assert.Single(entries, entry => entry.AssetId == "shape:funtasy-face");
            Assert.Equal("preset_shape", shape.AssetType);
            Assert.Equal("FUNtasy Face", shape.DisplayName);
            var plain = Assert.Single(entries, entry => entry.AssetId == "pose:one");
            Assert.Equal("Product A", plain.ImmediateFolderName);
            Assert.Equal("People/Genesis 8 Female/Poses/Product A", plain.RelativeFolderPath);
            Assert.Equal("Pose One", plain.DisplayName);
            Assert.Equal(previewPath, plain.PreviewImagePath);
            Assert.Contains("People/Genesis 8 Female/Poses/Product A/Subcategory", await index.GetFoldersAsync(root));
            Assert.Single(await index.SearchAsync(root, string.Empty, includeChildren: false, searchText: "Root Pose"));
            Assert.Equal(3, (await index.SearchAsync(root, string.Empty, includeChildren: true, searchText: "Pose")).Count);
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public async Task IncrementalScanUpdatesMetadataAndRemovesDeletedSource()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var root = Path.Combine(temp, "Library");
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "Product", "Pose.duf");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            WritePoseMetadata(source, "pose:update", "Before", compressed: false);
            var index = new LibraryIndexService(Path.Combine(temp, "index.db"));
            Assert.Equal(1, (await index.ScanAsync(root)).AddedOrUpdated);

            WritePoseMetadata(source, "pose:update", "After updated label", compressed: false);
            File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(2));
            Assert.Equal(1, (await index.ScanAsync(root)).AddedOrUpdated);
            Assert.Equal("After updated label", Assert.Single(await index.SearchAsync(root, "Product", false, "updated label")).DisplayName);

            File.Delete(source);
            Assert.Equal(1, (await index.ScanAsync(root)).Removed);
            Assert.Empty(await index.GetAllAsync(root));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public async Task SearchUsesCurrentFolderOrDescendantsAndIndexedFolderNames()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var root = Path.Combine(temp, "Library");
            var folder = Path.Combine(root, "Sitting", "Vintage Glamour");
            var child = Path.Combine(folder, "Product Set");
            Directory.CreateDirectory(child);
            WritePoseMetadata(Path.Combine(folder, "Pose 03.duf"), "pose:03", "Vintage Glamour Pose 03", compressed: false);
            WritePoseMetadata(Path.Combine(child, "Pose 04.duf"), "pose:04", "Pose 04", compressed: false);
            var index = new LibraryIndexService(Path.Combine(temp, "index.db"));
            await index.ScanAsync(root);

            Assert.Single(await index.SearchAsync(root, "Sitting/Vintage Glamour", includeChildren: false, searchText: "Pose"));
            Assert.Equal(2, (await index.SearchAsync(root, "Sitting/Vintage Glamour", includeChildren: true, searchText: "Pose")).Count);
            Assert.Single(await index.SearchAsync(root, "Sitting/Vintage Glamour", includeChildren: true, searchText: "Product Set"));
            Assert.Empty(await index.SearchAsync(root, "Sitting/Vintage Glamour", includeChildren: false, searchText: "Product Set"));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void OutputNamingUsesImmediateFolderAndStableCollisionSuffix()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var firstSource = Path.Combine(temp, "Vendor A", "Vintage Glamour", "Pose 03.duf");
            var secondSource = Path.Combine(temp, "Vendor B", "Vintage Glamour", "Pose 03.duf");
            Directory.CreateDirectory(Path.GetDirectoryName(firstSource)!);
            Directory.CreateDirectory(Path.GetDirectoryName(secondSource)!);
            File.WriteAllText(firstSource, "source A");
            File.WriteAllText(secondSource, "source B");
            var settings = SettingsFor(project);
            var naming = new PoseOutputNamingService(new UnityProjectService());

            var first = naming.Resolve(settings, firstSource, "Sitting/Romantic");
            Assert.Equal("Vintage Glamour - Pose 03", first.BaseName);
            Assert.Equal(first.CanonicalPath, naming.Resolve(settings, firstSource, "Sitting/Romantic").CanonicalPath);

            File.WriteAllText(first.CanonicalPath, JsonSerializer.Serialize(new { source = new { poseFile = firstSource } }));
            var collision = naming.Resolve(settings, secondSource, "Sitting/Romantic");
            var repeated = new PoseOutputNamingService(new UnityProjectService()).Resolve(settings, secondSource, "Sitting/Romantic");
            Assert.NotEqual(first.CanonicalPath, collision.CanonicalPath);
            Assert.Contains("[", collision.BaseName);
            Assert.Equal(collision.CanonicalPath, repeated.CanonicalPath);
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public void OutputAndDestinationNamesRemoveInvalidAndReservedCharacters()
    {
        var sanitizedFile = PoseOutputNamingService.SanitizeFileStem("Pose <>:\"/\\|?* .");
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars()) Assert.DoesNotContain(invalidCharacter, sanitizedFile);
        Assert.DoesNotContain('/', sanitizedFile);
        Assert.DoesNotContain('\\', sanitizedFile);
        Assert.False(sanitizedFile.EndsWith(".", StringComparison.Ordinal));
        Assert.StartsWith("_CON", UnityProjectService.SanitizeFolderName("CON"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('/', UnityProjectService.SanitizeFolderName("New/Folder"));
    }

    [Fact]
    public async Task DestinationCreationMakesMatchingFoldersAndWritesBridgeConfiguration()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var settings = SettingsFor(project);
            var service = new UnityProjectService();
            var relative = await service.CreateDestinationFolderAsync(settings, "Sitting", "Romantic: Poses");
            Assert.Equal("Sitting/Romantic_ Poses", relative);
            Assert.True(Directory.Exists(Path.Combine(project, "Assets", "Animations", "DazPoses", "Sitting", "Romantic_ Poses")));
            Assert.True(Directory.Exists(Path.Combine(project, "Assets", "DazPoseImports", "Sitting", "Romantic_ Poses")));
            using var bridge = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(project, "DazPoseWizard.project.json")));
            Assert.Equal("Assets/DazPoseImports", bridge.RootElement.GetProperty("importRoot").GetString());
            Assert.Equal("Assets/Animations/DazPoses", bridge.RootElement.GetProperty("outputRoot").GetString());
            Assert.Throws<ArgumentException>(() => service.NormalizeAssetRelativePath("Assets/../ProjectSettings"));
            Assert.Throws<ArgumentException>(() => service.ValidateAssetRoots("Assets/DazPoseImports", "Assets/DazPoseImports"));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public async Task SettingsPersistOutsideTheDazLibrary()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var settingsPath = Path.Combine(temp, "local-state", "settings.json");
            var service = new SettingsService(settingsPath);
            var settings = new AppSettings
            {
                DazContentRoot = Path.Combine(temp, "DAZ Library"),
                FigureDefinitionPath = Path.Combine(temp, "Genesis8Female.dsf"),
                UnityProjectRoot = Path.Combine(temp, "UnityProject"),
                FinalPoseAssetRoot = "Assets/Animations/DazPoses",
                CanonicalImportRoot = "Assets/DazPoseImports",
                LastSelectedSourceFolder = "Sitting/Product A",
                SearchIncludesChildren = false,
                ConvertedFilter = "Converted"
            };

            await service.SaveAsync(settings);
            var loaded = service.Load();

            Assert.Equal(settings.DazContentRoot, loaded.DazContentRoot);
            Assert.Equal(settings.UnityProjectRoot, loaded.UnityProjectRoot);
            Assert.Equal("Sitting/Product A", loaded.LastSelectedSourceFolder);
            Assert.False(loaded.SearchIncludesChildren);
            Assert.Equal("Converted", loaded.ConvertedFilter);
            Assert.DoesNotContain("DAZ Library", settingsPath, StringComparison.OrdinalIgnoreCase);
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public async Task RegistrySupportsMultipleOutputsAndRechecksAnimFileReality()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var settings = SettingsFor(project);
            var source = Path.Combine(temp, "Vintage Glamour", "Pose 03.duf");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, "unchanged source");
            var projectService = new UnityProjectService();
            await projectService.WriteBridgeConfigurationAsync(settings);
            var registry = new ConversionRegistryService(projectService);
            var first = WriteCanonical(project, "Sitting/Romantic", "Sitting Pose.dazpose.json", source);
            var second = WriteCanonical(project, "Standing/Reference", "Standing Pose.dazpose.json", source);
            var firstAnim = Path.Combine(project, "Assets", "Animations", "DazPoses", "Sitting", "Romantic", "Sitting Pose.anim");
            var secondAnim = Path.Combine(project, "Assets", "Animations", "DazPoses", "Standing", "Reference", "Standing Pose.anim");
            Directory.CreateDirectory(Path.GetDirectoryName(firstAnim)!);
            Directory.CreateDirectory(Path.GetDirectoryName(secondAnim)!);
            File.WriteAllText(firstAnim, "clip one");
            File.WriteAllText(secondAnim, "clip two");
            await registry.WriteStatusAsync(settings, StatusFor(first, "Sitting/Romantic", firstAnim, source, project));
            await registry.WriteStatusAsync(settings, StatusFor(second, "Standing/Reference", secondAnim, source, project));

            var initialOutputs = registry.Reconcile(settings)[Path.GetFullPath(source)];
            Assert.Equal(2, initialOutputs.Count);
            Assert.All(initialOutputs, output => Assert.Equal(ConversionJobState.Converted, output.State));
            var cache = new ConversionRegistryCacheService(Path.Combine(temp, "local-index.db"));
            cache.Replace(project, registry.Reconcile(settings));
            Assert.Equal(2, cache.Load(project)[Path.GetFullPath(source)].Count);

            File.Delete(firstAnim);
            var afterDelete = registry.Reconcile(settings)[Path.GetFullPath(source)];
            Assert.Equal(2, afterDelete.Count);
            Assert.Contains(afterDelete, output => output.State == ConversionJobState.AwaitingUnity);
            Assert.Contains(afterDelete, output => output.State == ConversionJobState.Converted);
            cache.Replace(project, registry.Reconcile(settings));
            var cachedAfterDelete = cache.Load(project)[Path.GetFullPath(source)];
            Assert.Contains(cachedAfterDelete, output => output.State == ConversionJobState.AwaitingUnity);
            Assert.Contains(cachedAfterDelete, output => output.State == ConversionJobState.Converted);

            File.Delete(second);
            var afterCanonicalDelete = registry.Reconcile(settings)[Path.GetFullPath(source)];
            Assert.Equal(2, afterCanonicalDelete.Count);
            Assert.Contains(afterCanonicalDelete, output => output.State == ConversionJobState.Converted);
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public async Task CanonicalOnlyQueueLeavesDazSourceUntouchedAndPublishesStagedJson()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var settings = SettingsFor(project);
            var figurePath = WriteFigureDefinition(Path.Combine(temp, "Genesis8Female.dsf"));
            settings.FigureDefinitionPath = figurePath;
            var source = WriteSimplePose(Path.Combine(temp, "Vendor", "Vintage Glamour", "Pose 03.duf"), "hip", "rotation", 15f);
            var originalBytes = await File.ReadAllBytesAsync(source);
            var originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));
            var originalMtime = File.GetLastWriteTimeUtc(source);
            var projectService = new UnityProjectService();
            var registry = new ConversionRegistryService(projectService);
            var naming = new PoseOutputNamingService(projectService);

            await using (var queue = new ConversionQueueService(() => settings, projectService, registry, naming))
            {
                await queue.EnqueueAsync([new ConversionRequest(source, "Sitting/Romantic")]);
                var job = Assert.Single(queue.Jobs);
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (job.State is ConversionJobState.Queued or ConversionJobState.Converting && DateTime.UtcNow < deadline)
                    await Task.Delay(25);
                Assert.True(job.State == ConversionJobState.AwaitingUnity, job.ErrorMessage);
                Assert.True(File.Exists(job.CanonicalPath));
                Assert.False(Directory.EnumerateFiles(Path.Combine(project, "Assets"), "*.bvh", SearchOption.AllDirectories).Any());
                Assert.False(Directory.EnumerateFiles(Path.Combine(project, "Assets"), "*.report.txt", SearchOption.AllDirectories).Any());
                Assert.Empty(Directory.EnumerateFiles(Path.Combine(project, "Library", "DazPoseWizard", "Staging"), "*.stage"));
                var registryState = registry.Reconcile(settings);
                var output = Assert.Single(registryState[Path.GetFullPath(source)]);
                Assert.Equal(ConversionJobState.AwaitingUnity, output.State);
                Directory.CreateDirectory(Path.GetDirectoryName(output.AnimPath)!);
                File.WriteAllText(output.AnimPath, "test clip output");
                Assert.Equal(ConversionJobState.Converted, Assert.Single(registry.Reconcile(settings)[Path.GetFullPath(source)]).State);
                File.Delete(output.AnimPath);
                Assert.Equal(ConversionJobState.AwaitingUnity, Assert.Single(registry.Reconcile(settings)[Path.GetFullPath(source)]).State);
            }

            Assert.True(File.Exists(source));
            Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source))));
            Assert.Equal(originalMtime, File.GetLastWriteTimeUtc(source));
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    [Fact]
    public async Task FailedCanonicalConversionLeavesNoImportJsonAndExposesDiagnostic()
    {
        var temp = FixtureData.NewTempDirectory();
        try
        {
            var project = CreateUnityProject(temp);
            var settings = SettingsFor(project);
            settings.FigureDefinitionPath = WriteFigureDefinition(Path.Combine(temp, "Genesis8Female.dsf"));
            var source = WriteSimplePose(Path.Combine(temp, "Vendor", "Vintage Glamour", "Unsupported.duf"), "@selection", "rotation", 3f);
            var projectService = new UnityProjectService();
            var registry = new ConversionRegistryService(projectService);
            var naming = new PoseOutputNamingService(projectService);
            await using var queue = new ConversionQueueService(() => settings, projectService, registry, naming);

            await queue.EnqueueAsync([new ConversionRequest(source, "Standing")]);
            var job = Assert.Single(queue.Jobs);
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (job.State is ConversionJobState.Queued or ConversionJobState.Converting && DateTime.UtcNow < deadline)
                await Task.Delay(25);

            Assert.Equal(ConversionJobState.Failed, job.State);
            Assert.Contains("Unsupported non-neutral channel", job.ErrorMessage);
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(project, "Assets", "DazPoseImports"), "*.dazpose.json", SearchOption.AllDirectories));
            var outputs = registry.Reconcile(settings);
            var failed = Assert.Single(outputs[Path.GetFullPath(source)]);
            Assert.Equal(ConversionJobState.Failed, failed.State);
            Assert.Contains("Unsupported non-neutral channel", failed.ErrorMessage);
        }
        finally { FixtureData.DeleteTempDirectory(temp); }
    }

    private static string WritePoseMetadata(string path, string id, string label, string type = "preset_pose", bool compressed = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new { asset_info = new { id, label, type } });
        if (!compressed) File.WriteAllText(path, json, new UTF8Encoding(false));
        else
        {
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            using var writer = new StreamWriter(gzip, new UTF8Encoding(false));
            writer.Write(json);
        }
        return path;
    }

    private static string WriteFigureDefinition(string path)
    {
        var json = """
            {"asset_info":{"id":"Genesis8Female"},"node_library":[
              {"id":"figure","name":"Genesis8Female","label":"Genesis 8 Female","type":"figure"},
              {"id":"hip","name":"hip","label":"hip","type":"bone","parent":"#figure","center_point":[{"id":"x","value":0},{"id":"y","value":0},{"id":"z","value":0}],"end_point":[{"id":"x","value":0},{"id":"y","value":1},{"id":"z","value":0}]}
            ]}
            """;
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }

    private static string WriteSimplePose(string path, string target, string property, float value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var address = target == "@selection" ? "@selection" : $"@selection/{target}";
        var json = JsonSerializer.Serialize(new
        {
            asset_info = new { id = Path.GetFileNameWithoutExtension(path), type = "preset_pose", label = "Simple Pose" },
            scene = new { animations = new[] { new { url = $"name://{address}:?{property}/x/value", keys = new[] { new[] { 0f, value } } } } }
        });
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }

    private static string CreateUnityProject(string temp)
    {
        var root = Path.Combine(temp, "UnityProject");
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        return root;
    }

    private static string WriteCanonical(string project, string destination, string name, string source)
    {
        var importPath = Path.Combine(project, "Assets", "DazPoseImports", destination.Replace('/', Path.DirectorySeparatorChar), name);
        Directory.CreateDirectory(Path.GetDirectoryName(importPath)!);
        File.WriteAllText(importPath, JsonSerializer.Serialize(new { source = new { poseFile = source } }));
        return importPath;
    }

    private static BrowserJobStatus StatusFor(string canonicalPath, string destination, string animPath, string source, string project) => new()
    {
        CanonicalImportPath = Path.GetRelativePath(project, canonicalPath).Replace('\\', '/'),
        SourcePosePath = source,
        DestinationRelativeFolder = destination,
        ExpectedAnimPath = Path.GetRelativePath(project, animPath).Replace('\\', '/'),
        State = nameof(ConversionJobState.Converted),
        Timestamp = DateTimeOffset.UtcNow
    };

    private static AppSettings SettingsFor(string project) => new()
    {
        UnityProjectRoot = project,
        FinalPoseAssetRoot = "Assets/Animations/DazPoses",
        CanonicalImportRoot = "Assets/DazPoseImports"
    };
}
