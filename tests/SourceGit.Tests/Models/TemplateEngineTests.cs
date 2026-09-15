using System.Collections.Generic;
using System.Linq;
using SourceGit.Models;
using Xunit;

namespace SourceGit.Tests.Models
{
    public class TemplateEngineTests
    {
        private static Branch MakeBranch(string name = "feature/my-feature") => new() { Name = name, IsLocal = true };

        private static List<Change> MakeChanges(params string[] paths)
        {
            return paths.Select(p => new Change { Path = p }).ToList();
        }

        private readonly TemplateEngine _engine = new();

        [Fact]
        public void Eval_ReturnsPlainText_Unchanged()
        {
            var result = _engine.Eval("chore: update dependencies", MakeBranch(), MakeChanges());
            Assert.Equal("chore: update dependencies", result);
        }

        [Fact]
        public void Eval_ReturnsEmpty_ForEmptyTemplate()
        {
            var result = _engine.Eval("", MakeBranch(), MakeChanges());
            Assert.Equal("", result);
        }

        [Fact]
        public void Eval_SubstitutesBranchName()
        {
            var result = _engine.Eval("Branch: ${branch_name}", MakeBranch("main"), MakeChanges());
            Assert.Equal("Branch: main", result);
        }

        [Fact]
        public void Eval_SubstitutesFilesNum()
        {
            var result = _engine.Eval("${files_num} files changed", MakeBranch(), MakeChanges("a.cs", "b.cs", "c.cs"));
            Assert.Equal("3 files changed", result);
        }

        [Fact]
        public void Eval_SubstitutesFiles_AsCommaSeparatedPaths()
        {
            var result = _engine.Eval("${files}", MakeBranch(), MakeChanges("src/a.cs", "src/b.cs"));
            Assert.Equal("src/a.cs, src/b.cs", result);
        }

        [Fact]
        public void Eval_SubstitutesPureFiles_AsFilenamesOnly()
        {
            var result = _engine.Eval("${pure_files}", MakeBranch(), MakeChanges("src/foo/a.cs", "src/bar/b.cs"));
            Assert.Equal("a.cs, b.cs", result);
        }

        [Fact]
        public void Eval_SlicedFiles_ShowsFirstN_PlusOverflowMessage()
        {
            var changes = MakeChanges("a.cs", "b.cs", "c.cs", "d.cs");
            var result = _engine.Eval("${files:2}", MakeBranch(), changes);
            Assert.Equal("a.cs, b.cs and 2 other files", result);
        }

        [Fact]
        public void Eval_SlicedFiles_ShowsAll_WhenCountExceedsTotal()
        {
            var changes = MakeChanges("a.cs", "b.cs");
            var result = _engine.Eval("${files:10}", MakeBranch(), changes);
            Assert.Equal("a.cs, b.cs", result);
        }

        [Fact]
        public void Eval_SlicedPureFiles_ShowsFilenamesOnly_WithOverflow()
        {
            var changes = MakeChanges("src/a.cs", "src/b.cs", "src/c.cs");
            var result = _engine.Eval("${pure_files:1}", MakeBranch(), changes);
            Assert.Equal("a.cs and 2 other files", result);
        }

        [Fact]
        public void Eval_ReplacesUnknownVariable_WithEmptyString()
        {
            var result = _engine.Eval("${unknown_var}", MakeBranch(), MakeChanges());
            Assert.Equal("", result);
        }

        [Fact]
        public void Eval_EscapedDollar_ProducesLiteralDollar()
        {
            var result = _engine.Eval(@"\${branch_name}", MakeBranch("main"), MakeChanges());
            Assert.Equal("${branch_name}", result);
        }

        [Fact]
        public void Eval_RegexVariable_ReplacesMatchInValue()
        {
            var result = _engine.Eval("${branch_name/feature\\//}", MakeBranch("feature/login"), MakeChanges());
            Assert.Equal("login", result);
        }

        [Fact]
        public void Eval_RegexVariable_ReturnsOriginalValue_WhenPatternDoesNotMatch()
        {
            var result = _engine.Eval("${branch_name/hotfix\\//}", MakeBranch("feature/login"), MakeChanges());
            Assert.Equal("feature/login", result);
        }

        [Theory]
        [InlineData("$branch_name", "$branch_name")] // $ without {
        [InlineData("${branch_name", "${branch_name")] // missing closing brace
        public void Eval_IncompleteSyntax_IsReturnedAsLiteralText(string template, string expected)
        {
            Assert.Equal(expected, _engine.Eval(template, MakeBranch("main"), MakeChanges()));
        }
    }
}
