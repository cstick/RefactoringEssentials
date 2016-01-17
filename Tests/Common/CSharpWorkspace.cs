using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RefactoringEssentials.Tests.Common
{
    static class CSharpHelpers
    {
        public static ParseOptions DefaultParseOptions = new CSharpParseOptions(
                    LanguageVersion.CSharp6,
                    DocumentationMode.Diagnose | DocumentationMode.Parse,
                    SourceCodeKind.Regular,
                    ImmutableArray.Create("DEBUG", "TEST")
                );

        static readonly MetadataReference mscorlib = MetadataReference.CreateFromFile(typeof(Console).Assembly.Location);
        static readonly MetadataReference systemAssembly = MetadataReference.CreateFromFile(typeof(System.ComponentModel.BrowsableAttribute).Assembly.Location);
        static readonly MetadataReference systemXmlLinq = MetadataReference.CreateFromFile(typeof(System.Xml.Linq.XElement).Assembly.Location);
        static readonly MetadataReference systemCore = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        static readonly MetadataReference[] DefaultMetadataReferences = {
                mscorlib,
                systemAssembly,
                systemCore,
                systemXmlLinq
            };

        public static SolutionInfo CreateSolution(SolutionId solutionId, IEnumerable<ProjectInfo> projects)
        {
            return SolutionInfo.Create(
                solutionId,
                VersionStamp.Create(),
                null,
                projects);
        }

        public static ProjectInfo CreateProject(ProjectId projectId, string name)
        {
            return ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    name,
                    name,
                    LanguageNames.CSharp,
                    null,
                    null,
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        "",
                        "",
                        "Script",
                        null,
                        OptimizationLevel.Debug,
                        false,
                        true
                    ),
                    DefaultParseOptions,
                    null,
                    null,
                    DiagnosticTestBase.DefaultMetadataReferences
                ).WithMetadataReferences(DefaultMetadataReferences);
        }

        public static ProjectInfo CreateProject(ProjectId projectId, string name, IEnumerable<DocumentInfo> documents)
        {
            return ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    name,
                    name,
                    LanguageNames.CSharp,
                    null,
                    null,
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        "",
                        "",
                        "Script",
                        null,
                        OptimizationLevel.Debug,
                        false,
                        true
                    ),
                    DefaultParseOptions,
                    documents,
                    null,
                    DiagnosticTestBase.DefaultMetadataReferences
                ).WithMetadataReferences(DefaultMetadataReferences);
        }

        public static DocumentInfo CreateDocument(DocumentId documentId, string name, string text)
        {
            if (!Path.HasExtension(name))
            {
                name = Path.ChangeExtension(name, ".cs");
            }

            return DocumentInfo.Create(
                    documentId,
                    name,
                    null,
                    SourceCodeKind.Regular,
                    TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create()))
                    );
        }

        public static Workspace RunContextAction<T>(Workspace workspace) where T : CodeRefactoringProvider, new()
        {
            var emptySpan = new TextSpan();

            var sln = workspace.CurrentSolution;

            Document doc = null;
            TextSpan selectedSpan = new TextSpan();
            TextSpan markedSpan = new TextSpan();

            foreach (var project in sln.Projects)
            {
                foreach (var document in project.Documents)
                {
                    TextSpan newSelectedSpan;
                    TextSpan newMarkedSpan;
                    var txt = Utils.ParseText(document.GetTextAsync().Result.ToString(), out newSelectedSpan, out newMarkedSpan);
                    if (newSelectedSpan != emptySpan || markedSpan != emptySpan)
                    {
                        if (doc != null)
                            Assert.Fail("Multiple text spans must not be chosen.");

                        sln = sln.WithDocumentText(document.Id, SourceText.From(txt));
                        workspace.TryApplyChanges(sln);

                        doc = workspace.CurrentSolution.GetDocument(document.Id);
                        selectedSpan = newSelectedSpan;
                        markedSpan = newMarkedSpan;
                    }
                }
            }

            if (doc == null)
                Assert.Fail("Text spans must be chosen.");



            var actions = new List<CodeAction>();
            var context = new CodeRefactoringContext(doc, selectedSpan, actions.Add, default(CancellationToken));

            var action = new T();
            action.ComputeRefactoringsAsync(context).Wait();

            if (markedSpan.Start > 0)
            {
                foreach (var nra in actions.OfType<NRefactoryCodeAction>())
                {
                    Assert.AreEqual(markedSpan, nra.TextSpan, "Activation span does not match.");
                }
            }

            //if (actions.Count < actionIndex)
            //    Console.WriteLine("invalid input is:" + input);

            var a = actions[0];
            foreach (var op in a.GetOperationsAsync(default(CancellationToken)).Result)
            {
                op.Apply(workspace, default(CancellationToken));
            }

            return workspace;
        }

        public static void AssertEqual(this Workspace expected, Workspace actual)
        {
            var expectedSln = expected.CurrentSolution;
            var actualSln = actual.CurrentSolution;

            if (expectedSln.Projects.Count() != actualSln.Projects.Count())
                Assert.Fail($"Project counts do not match. Expected; {expectedSln.Projects.Count()}, Actual: {actualSln.Projects.Count()}.");

            foreach (var expProject in expectedSln.Projects)
            {
                var actProject = actualSln.GetProject(expProject.Id);
                if (actProject == null)
                    Assert.Fail($"Project with ID \"{ expProject.Id}\" and name\"{expProject.Name}\" does not exist.");

                if (expProject.Documents.Count() != actProject.Documents.Count())
                    Assert.Fail($"Document counts do not match for project named \"{expProject.Name}\". Expected; {expProject.Documents.Count()}, Actual: {actProject.Documents.Count()}.");

                foreach (var expDoc in expProject.Documents)
                {
                    var actDoc = actProject.GetDocument(expDoc.Id);
                    if (actDoc == null)
                        Assert.Fail($"Document with ID \"{ expDoc.Id}\" and name\"{expDoc.Name}\" does not exist.");

                    var expText = Utils.HomogenizeEol(expDoc.GetTextAsync().Result.ToString());
                    var actText = Utils.HomogenizeEol(actDoc.GetTextAsync().Result.ToString());

                    if (!string.Equals(expText, actText))
                        Assert.Fail($"Document with name\"{expDoc.Name}\" does not match. Expected:\n{expText}\nActual:\n{actText}\n");
                }
            }
        }

        public static void AssertEqual(Solution expected, Solution actual)
        {
            var expectedSln = expected.CurrentSolution;
            var actualSln = actual.CurrentSolution;

            if (expectedSln.Projects.Count() != actualSln.Projects.Count())
                Assert.Fail($"Project counts do not match. Expected; {expectedSln.Projects.Count()}, Actual: {actualSln.Projects.Count()}.");

            foreach (var expProject in expectedSln.Projects)
            {
                var actProject = actualSln.GetProject(expProject.Id);
                if (actProject == null)
                    Assert.Fail($"Project with ID \"{ expProject.Id}\" and name\"{expProject.Name}\" does not exist.");

                if (expProject.Documents.Count() != actProject.Documents.Count())
                    Assert.Fail($"Document counts do not match for project named \"{expProject.Name}\". Expected; {expProject.Documents.Count()}, Actual: {actProject.Documents.Count()}.");

                foreach (var expDoc in expProject.Documents)
                {
                    var actDoc = actProject.GetDocument(expDoc.Id);
                    if (actDoc == null)
                        Assert.Fail($"Document with ID \"{ expDoc.Id}\" and name\"{expDoc.Name}\" does not exist.");

                    var expText = Utils.HomogenizeEol(expDoc.GetTextAsync().Result.ToString());
                    var actText = Utils.HomogenizeEol(actDoc.GetTextAsync().Result.ToString());

                    if (!string.Equals(expText, actText))
                        Assert.Fail($"Document with name\"{expDoc.Name}\" does not match. Expected:\n{expText}\nActual:\n{actText}\n");
                }
            }
        }
    }
}
