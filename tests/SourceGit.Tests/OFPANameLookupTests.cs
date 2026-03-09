using SourceGit.Utilities;

namespace SourceGit.Tests;

public class OFPANameLookupTests
{
    [Fact]
    public async Task LookupWorkingTreeAsync_WithWorkingTreeCandidate_ReturnsDecodedName()
    {
        var repoDir = OFPATestHelpers.CreateTempDirectory();
        try
        {
            var relativePath = "Content/__ExternalActors__/Maps/Test/J28ZVKRUOZJY0PHKR205X.uasset";
            OFPATestHelpers.CopyTestAsset(repoDir, "UE5_3/J28ZVKRUOZJY0PHKR205X.uasset", relativePath);
            var fullPath = Path.Combine(repoDir, relativePath);

            var results = await OFPANameLookup.LookupWorkingTreeAsync(repoDir, [
                new OFPANameLookup.WorkingTreeCandidate(relativePath, fullPath, $":{relativePath}", $"HEAD:{relativePath}")
            ]);

            Assert.Equal("BP_IntroCameraActor2", results[relativePath]);
        }
        finally
        {
            OFPATestHelpers.DeleteDirectory(repoDir);
        }
    }

    [Fact]
    public async Task LookupWorkingTreeAsync_WithMissingWorkingTreeFile_FallsBackToIndex()
    {
        var repoDir = OFPATestHelpers.CreateRepository();
        try
        {
            var relativePath = "Content/__ExternalActors__/Maps/Test/J28ZVKRUOZJY0PHKR205X.uasset";
            OFPATestHelpers.CopyTestAsset(repoDir, "UE5_3/J28ZVKRUOZJY0PHKR205X.uasset", relativePath);
            OFPATestHelpers.RunGit(repoDir, $"add -- \"{relativePath}\"");
            File.Delete(Path.Combine(repoDir, relativePath));

            var results = await OFPANameLookup.LookupWorkingTreeAsync(repoDir, [
                new OFPANameLookup.WorkingTreeCandidate(relativePath, string.Empty, $":{relativePath}", $"HEAD:{relativePath}")
            ]);

            Assert.Equal("BP_IntroCameraActor2", results[relativePath]);
        }
        finally
        {
            OFPATestHelpers.DeleteDirectory(repoDir);
        }
    }

    [Fact]
    public async Task LookupWorkingTreeAsync_WithDeletedIndex_FallsBackToHead()
    {
        var repoDir = OFPATestHelpers.CreateRepository();
        try
        {
            var relativePath = "Content/__ExternalActors__/Maps/Test/J28ZVKRUOZJY0PHKR205X.uasset";
            OFPATestHelpers.CopyTestAsset(repoDir, "UE5_3/J28ZVKRUOZJY0PHKR205X.uasset", relativePath);
            OFPATestHelpers.RunGit(repoDir, $"add -- \"{relativePath}\"");
            OFPATestHelpers.RunGit(repoDir, "commit -m initial");
            OFPATestHelpers.RunGit(repoDir, $"rm -- \"{relativePath}\"");

            var results = await OFPANameLookup.LookupWorkingTreeAsync(repoDir, [
                new OFPANameLookup.WorkingTreeCandidate(relativePath, string.Empty, $":{relativePath}", $"HEAD:{relativePath}")
            ]);

            Assert.Equal("BP_IntroCameraActor2", results[relativePath]);
        }
        finally
        {
            OFPATestHelpers.DeleteDirectory(repoDir);
        }
    }

    [Fact]
    public async Task LookupRevisionObjectsAsync_WithCommitDerivedSpec_ReturnsDecodedName()
    {
        var repoDir = OFPATestHelpers.CreateRepository();
        try
        {
            var relativePath = "Content/__ExternalActors__/Maps/Test/TIK1LLNYUFCW2RY3OQGQCH.uasset";
            OFPATestHelpers.CopyTestAsset(repoDir, "UE5_6/TIK1LLNYUFCW2RY3OQGQCH.uasset", relativePath);
            OFPATestHelpers.RunGit(repoDir, $"add -- \"{relativePath}\"");
            OFPATestHelpers.RunGit(repoDir, "commit -m initial");
            var head = OFPATestHelpers.RunGit(repoDir, "rev-parse HEAD");

            var results = await OFPANameLookup.LookupRevisionObjectsAsync(repoDir, [
                new OFPANameLookup.RevisionObjectSpec(relativePath, $"{head}:{relativePath}")
            ]);

            Assert.Equal("LandscapeStreamingProxy_7_2_0", results[relativePath]);
        }
        finally
        {
            OFPATestHelpers.DeleteDirectory(repoDir);
        }
    }

    [Fact]
    public async Task LookupRevisionObjectsAsync_WithStashDerivedSpec_ReturnsDecodedName()
    {
        var repoDir = OFPATestHelpers.CreateRepository();
        try
        {
            var relativePath = "Content/__ExternalActors__/Maps/Test/QD0WQDX4NT49M879U915NN.uasset";
            OFPATestHelpers.CopyTestAsset(repoDir, "UE5_3/J28ZVKRUOZJY0PHKR205X.uasset", relativePath);
            OFPATestHelpers.RunGit(repoDir, $"add -- \"{relativePath}\"");
            OFPATestHelpers.RunGit(repoDir, "commit -m initial");
            OFPATestHelpers.CopyTestAsset(repoDir, "UE5_7/QD0WQDX4NT49M879U915NN.uasset", relativePath);
            OFPATestHelpers.RunGit(repoDir, $"stash push -m ofpa -- \"{relativePath}\"");

            var results = await OFPANameLookup.LookupRevisionObjectsAsync(repoDir, [
                new OFPANameLookup.RevisionObjectSpec(relativePath, $"stash@{{0}}:{relativePath}")
            ]);

            Assert.Equal("Lighting", results[relativePath]);
        }
        finally
        {
            OFPATestHelpers.DeleteDirectory(repoDir);
        }
    }
}
