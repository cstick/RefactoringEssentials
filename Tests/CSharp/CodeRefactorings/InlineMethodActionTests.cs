using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using RefactoringEssentials.CSharp.CodeRefactorings.Uncategorized;
using RefactoringEssentials.Tests.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringEssentials.Tests.CSharp.CodeRefactorings
{
    [TestFixture]
    public class InlineMethodActionTests : CSharpCodeRefactoringTestBase
    {

        [Test]
        [Description("Test that only event field declarations are refactored.")]
        public void InlineSimpleMethod()
        {
            Test<InlineMethodAction>(
@"class TestClass
{
    public void Foo() {
        var x = Bar(5, 1);
    }

    public int $Bar(int a, int b) {
        //// Simple math.
        return (a + b);
    }

}",
@"class TestClass
{
    public void Foo() {
        var x = (5 + 1);
    }

}");
        }

        [Test]
        [Description("Test that only event field declarations are refactored.")]
        public void InlineSimpleMethodMultipleReferences()
        {
            Test<InlineMethodAction>(
@"class TestClass
{
    public void Foo() {
        var x = Bar(5, 1);
        var x2 = Bar(10, 2);
    }

    public int $Bar(int a, int b) {
        return (a + b);
    }

}",
@"class TestClass
{
    public void Foo() {
        var x = (5 + 1);
        var x2 = (10 + 2);
    }

}");
        }

        [Test]
        [Description("Test that only event field declarations are refactored.")]
        public void InlineSimpleMethodWithOptionalParam()
        {
            Test<InlineMethodAction>(
@"class TestClass
{
    public void Foo() {
        var x = Bar(5, 1);
    }

    public int $Bar(int a, int b, int c = 0) {
        //// Simple math.
        return (a + b + c);
    }

}",
@"class TestClass
{
    public void Foo() {
        var x = (5 + 1 + 0);
    }

}");
        }

        [Test]
        [Description("Test that only event field declarations are refactored.")]
        public void MultipleDocumentTest()
        {
            var solutionId = SolutionId.CreateNewId();
            var projectId = ProjectId.CreateNewId();
            var projectName = "ProjectA";
            var document1Id = DocumentId.CreateNewId(projectId);
            var document1Name = "ClassA";
            var document2Id = DocumentId.CreateNewId(projectId);
            var document2Name = "ClassB";

            var testSolution = CSharpHelpers.CreateSolution(solutionId, new[] {
                CSharpHelpers.CreateProject(projectId, projectName, new[] {
                    CSharpHelpers.CreateDocument(document1Id, document1Name,
@"class ClassA
{
    public void Foo() {
        var x = ClassB.Bar(5, 1);
    }
}"),
                    CSharpHelpers.CreateDocument(document2Id, document2Name,
@"class ClassB
{
    public void Foo() {
        var x = Bar(10, 2);
    }

    public static int $Bar(int a, int b, int c = 0) {
        //// Simple math.
        return (a + b + c);
    }

    public void Foo2() {
        var x = Bar(10, 2);
    }
}") })
                });

            var expSolution = CSharpHelpers.CreateSolution(
                solutionId,
                new[] {
                    CSharpHelpers.CreateProject(
                        projectId,
                        projectName,
                        new []
                        {
                            CSharpHelpers.CreateDocument(document1Id,document1Name,
@"class ClassA
{
    public void Foo() {
        var x = (5 + 1 + 0);
    }
}"),
                            CSharpHelpers.CreateDocument(document2Id,document2Name ,
@"class ClassB
{
    public void Foo() {
        var x = (10 + 2 + 0);
    }

    public void Foo2() {
        var x = (10 + 2 + 0);
    }
}")
                        })
            });
            
            var w = new AdhocWorkspace(MefHostServices.DefaultHost, "Temp");
            w.AddSolution(testSolution);

            var alteredWorkspace = CSharpHelpers.RunContextAction<InlineMethodAction>(w);

            var expWorkspace = new AdhocWorkspace();
            expWorkspace.AddSolution(expSolution);

            CSharpHelpers.AssertEqual(expWorkspace, alteredWorkspace);
        }

        public static int Foo(int a, int b = 0)
        {
            return (a + b);
        }

    }
}
