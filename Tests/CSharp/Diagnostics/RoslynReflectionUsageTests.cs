using NUnit.Framework;
using RefactoringEssentials.CSharp;

namespace RefactoringEssentials.Tests.CSharp.Diagnostics
{
    [TestFixture]
    public class RoslynReflectionUsageTests : CSharpDiagnosticTestBase
    {
        const string attributeFakes = @"
using System;
using RefactoringEssentials;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace RefactoringEssentials
{
    [Flags]
    enum RoslynReflectionAllowedContext
    {
        None = 0,
        Analyzers = 1,
        CodeFixes = 2
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    class RoslynReflectionUsageAttribute : Attribute
    {
        public RoslynReflectionUsageAttribute(RoslynReflectionAllowedContext allowedContexts = RoslynReflectionAllowedContext.None)
        {
            AllowedContexts = allowedContexts;
        }

        public RoslynReflectionAllowedContext AllowedContexts { get; set; }
    }
}
namespace Microsoft.CodeAnalysis.Diagnostics
{
    [AttributeUsage(AttributeTargets.Class)]
    class DiagnosticAnalyzerAttribute : Attribute
    {
    }
}
namespace Microsoft.CodeAnalysis.CodeFixes
{
    [AttributeUsage(AttributeTargets.Class)]
    class ExportCodeFixProviderAttribute : Attribute
    {
    }
}
namespace Microsoft.CodeAnalysis.CodeRefactorings
{
    [AttributeUsage(AttributeTargets.Class)]
    class ExportCodeRefactoringProviderAttribute : Attribute
    {
    }
}
";

        [Test]
        public void ForbiddenMethodInAnalyzer()
        {
            Analyze<RoslynReflectionUsageAnalyzer>(attributeFakes + @"
static class SomeUtilityClass
{
    [RoslynReflectionUsage(RoslynReflectionAllowedContext.CodeFixes)]
    public static void ForbiddenMethod(this int i)
    {
    }
}

[DiagnosticAnalyzer]
public class TestAnalyzer : DiagnosticAnalyzer
{
    public TestAnalyzer()
    {
        int i = 0;
        i.$ForbiddenMethod$();
    }
}");
        }

        [Test]
        public void ForbiddenClassInAnalyzer()
        {
            Analyze<RoslynReflectionUsageAnalyzer>(attributeFakes + @"
[RoslynReflectionUsage(RoslynReflectionAllowedContext.CodeFixes)]
static class SomeUtilityClass
{
    public static void ForbiddenMethod(this int i)
    {
    }
}

[DiagnosticAnalyzer]
public class TestAnalyzer : DiagnosticAnalyzer
{
    public TestAnalyzer()
    {
        int i = 0;
        i.$ForbiddenMethod$();
    }
}");
        }

        [Test]
        public void ForbiddenMethodInCodeFix()
        {
            Analyze<RoslynReflectionUsageAnalyzer>(attributeFakes + @"
static class SomeUtilityClass
{
    [RoslynReflectionUsage(RoslynReflectionAllowedContext.Analyzers)]
    public static void ForbiddenMethod(this int i)
    {
    }
}

[ExportCodeFixProvider]
public class TestCodeFix : CodeFixProvider
{
    public TestCodeFix()
    {
        int i = 0;
        i.$ForbiddenMethod$();
    }
}");
        }

        [Test]
        public void ForbiddenMethodInRefactoring()
        {
            Analyze<RoslynReflectionUsageAnalyzer>(attributeFakes + @"
static class SomeUtilityClass
{
    [RoslynReflectionUsage(RoslynReflectionAllowedContext.Analyzers)]
    public static void ForbiddenMethod(this int i)
    {
    }
}

[ExportCodeRefactoringProvider]
public class TestRefactoring : CodeRefactoringProvider
{
    public TestRefactoring()
    {
        int i = 0;
        i.$ForbiddenMethod$();
    }
}");
        }

        [Test]
        public void AllowedMethodInAnalyzer1()
        {
            Analyze<RoslynReflectionUsageAnalyzer>(attributeFakes + @"
static class SomeUtilityClass
{
    [RoslynReflectionUsage(RoslynReflectionAllowedContext.Analyzers)]
    public static void ForbiddenMethod(this int i)
    {
    }
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestAnalyzer : DiagnosticAnalyzer
{
    public TestAnalyzer()
    {
        int i = 0;
        i.ForbiddenMethod();
    }
}");
        }

        [Test]
        public void AllowedMethodInAnalyzer2()
        {
            Analyze<RoslynReflectionUsageAnalyzer>(attributeFakes + @"
static class SomeUtilityClass
{
    [RoslynReflectionUsage(RoslynReflectionAllowedContext.Analyzers | RoslynReflectionAllowedContext.CodeFixes)]
    public static void ForbiddenMethod(this int i)
    {
    }
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestAnalyzer : DiagnosticAnalyzer
{
    public TestAnalyzer()
    {
        int i = 0;
        i.ForbiddenMethod();
    }
}");
        }

        [Test]
        public void MethodNotInAnalyzer()
        {
            Analyze<RoslynReflectionUsageAnalyzer>(attributeFakes + @"
static class SomeUtilityClass
{
    [RoslynReflectionUsage(RoslynReflectionAllowedContext.Analyzers)]
    public static void ForbiddenMethod(this int i)
    {
    }
}

public class TestAnalyzer : DiagnosticAnalyzer
{
    public TestAnalyzer()
    {
        int i = 0;
        i.ForbiddenMethod();
    }
}");
        }
    }
}

