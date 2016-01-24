using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using NUnit.Framework;
using RefactoringEssentials.CSharp.CodeRefactorings.Uncategorized;
using RefactoringEssentials.Tests.Common;

namespace RefactoringEssentials.Tests.CSharp.CodeRefactorings
{
    [TestFixture]
    public class InlineMethodActionTests : CSharpCodeRefactoringTestBase
    {
        [Test]
        [Description("Do not suggest refactoring unless the cursor is over the method signature (excluding arguments).")]
        public void DoesNotRefactorWhenNotOnMethodSignature()
        {
            TestWrongContext<InlineMethodAction>(
@"
class Animal
{
    public void Foo() {
        var x = Bar(5, 1);
    }

    public int Bar(int a, int b) {
        //// Simple math.
        return $(a + b);
    }

}");

            TestWrongContext<InlineMethodAction>(
@"
class Animal
{
    public void Foo() {
        var x = Bar(5, 1);
    }

    public int Bar(int a, int b) {
        //// Simple math.
        $return (a + b);
    }

}");

            TestWrongContext<InlineMethodAction>(
@"
class Animal
{
    public void Foo() {
        var x = Bar(5, 1);
    }

    public int Bar(int a, int $b) {
        //// Simple math.
        return (a + b);
    }

}");
        }

        [Test]
        [Description("Do not inline a method with errors, that would just make more errors.")]
        public void DoesNotRefactorMethodWithErrors()
        {
            TestWrongContext<InlineMethodAction>(
@"
class Animal
{
    public void Foo() {
        var x = Bar(5, 1);
    }

    public int $Bar(int a, int b) {
        //// Simple math.
        return (a + b) This is invalid syntax!;
    }

}");
        }

        [Test]
        [Description("Do not inline an external public method. It may be referenced externally and refactoring may be breaking.")]
        public void DoesNotRefactorPublicExternalMethods()
        {
            TestWrongContext<InlineMethodAction>(
@"
public class Animal
{
    public void Foo() {
        var x = Bar(5, 1);
    }

    public int $Bar(int a, int b) {
        //// Simple math.
        return (a + b);
    }

}");
        }

        [Test]
        [Description("Do not inline a method without any references, that is not our interest.")]
        public void DoesNotRefactorUnreferencedMethods()
        {
            TestWrongContext<InlineMethodAction>(
@"
class Animal
{

    // Method without any references.
    public int $Bar(int a, int b) {
        //// Simple math.
        return (a + b);
    }
}");
        }

        [Test]
        [Description("Do not inline a method with more than 1 line of code. This test may be invalid as the capability of method inlining is expanded.")]
        public void DoesNotRefactorComplexMethod()
        {
            TestWrongContext<InlineMethodAction>(
@"
class Animal
{
    public void Foo() {
        var x = Bar(1, 2);
    }

    // A complex method, one with multiple lines of code.
    public int $Bar(int a, int b) {
        //// Simple math.
        var result = (a + b);
        return result;
    }
}");
        }

        [Test]
        [Description("Test that inline method is not refactored when contains internal member access.")]
        public void DoNotInlineMethodBecauseOfInternalMemberAccess()
        {
            TestWrongContext<InlineMethodAction>(
@"class Dog : Animal {
    
    public void DoLegs() {
        AddLeg();
        AddLeg();
        AddLeg();
        AddLeg();
    }
}

class Animal
{
    private int legs = 0;
    
    protected void $AddLeg() {
        //// Simple math.
        legs++;
    }

}");
        }

        [Test]
        [Description("Inline a method with return type.")]
        public void InlineReturnMethod()
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
        [Description("Inline a method without return type.")]
        public void InlineVoidMethod()
        {
            Test<InlineMethodAction>(
@"class TestClass
{
    public void Foo() {
        Bar(5, 1);
    }

    public void $Bar(int a, int b) {
        //// Simple math.
        Console.Writeline(a + b);
    }

}",
@"class TestClass
{
    public void Foo() {
        Console.Writeline(5 + 1);
    }

}");
        }
        
        [Test]
        [Description("Inline a method with multiple references.")]
        public void InlineMethodWithMultipleReferences()
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
        [Description("Inline a method with optional parameters.")]
        public void InlineMethodWithOptionalParam()
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
        [Description("Overly complicated test to ensure refactorings cross documents.")]
        public void MultipleDocumentTest()
        {
            var solutionId = SolutionId.CreateNewId();
            var projectId = ProjectId.CreateNewId();
            var projectName = "ProjectA";
            var document1Id = DocumentId.CreateNewId(projectId);
            var document1Name = "ClassA";
            var document2Id = DocumentId.CreateNewId(projectId);
            var document2Name = "ClassB";

            // Create Test Workspace
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

            var w = new AdhocWorkspace(MefHostServices.DefaultHost, "Temp");
            w.AddSolution(testSolution);

            // Create expected workspace
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

            var expWorkspace = new AdhocWorkspace();
            expWorkspace.AddSolution(expSolution);

            // Run action
            var alteredWorkspace = CSharpHelpers.RunContextAction<InlineMethodAction>(w);

            // Assert expected workspace equals altered workspace.
            CSharpHelpers.AssertEqual(expWorkspace, alteredWorkspace);
        }

    }
}
