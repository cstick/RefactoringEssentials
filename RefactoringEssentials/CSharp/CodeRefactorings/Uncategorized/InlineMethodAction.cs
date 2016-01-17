using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RefactoringEssentials.CSharp.CodeRefactorings.Uncategorized
{
    [ExportCodeRefactoringProvider(
        LanguageNames.CSharp,
        Name = @"Put the method's body into the body of its callers and remove the method.")]
    public class InlineMethodAction : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var span = context.Span;
            var cancellationToken = context.CancellationToken;

            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model.IsFromGeneratedCode(cancellationToken))
                return;

            var root = await model.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var node = root.FindNode(span);
            if (node == null)
                return;
            // MethodDeclaration, InvocationExpression

            var method = node as MethodDeclarationSyntax;

            if (method == null)
            {
                method = (node?.Parent ?? null) as MethodDeclarationSyntax;
            }

            if (method != null)
            {
                if (method.ContainsDiagnostics)
                    return;

                var methodSymbol = model.GetDeclaredSymbol(method);

                // If the method is public then it may be referenced externally.
                // This is subjective and some may disagree with this choice of behavior.
                var isPublic = true;
                ISymbol cs = methodSymbol;
                do
                {
                    if (cs.DeclaredAccessibility == Accessibility.Public ||
                        cs.DeclaredAccessibility == Accessibility.NotApplicable)
                        cs = cs.ContainingSymbol;
                    else
                        isPublic = false;

                } while (isPublic && cs != null);

                if (isPublic)
                    return;

                // Get references to the method.
                var methodReferences = await SymbolFinder.FindReferencesAsync(methodSymbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);

                // Ensure there are references.
                if (!methodReferences.Any())
                    return;

                var isSimpleMethodBody = method.Body.Statements.Count() == 1;

                if (isSimpleMethodBody)
                {
                    context.RegisterRefactoring(
                        CodeAction.Create(
                            "Inline Method",
                            ct => ProcessMethodDeclarationAsync(
                                document.Project.Solution,
                                methodSymbol,
                                method,
                                ct)));
                }
            }
        }

        async Task<Solution> ProcessMethodDeclarationAsync(
            Solution sln,
            ISymbol methodSymbol,
            MethodDeclarationSyntax method,
            CancellationToken ct)
        {
            var methodParams = method.ParameterList.Parameters;

            SyntaxNode methodBody = method.Body.Statements.First();

            if (methodBody.IsKind(SyntaxKind.ReturnStatement))
            {
                var returnStatement = methodBody as ReturnStatementSyntax;
                methodBody = returnStatement.Expression;
            }

            //var documentChanges = new List<DocumentChange>();

            var references = SymbolFinder.FindReferencesAsync(methodSymbol, sln).Result;

            foreach (var reference in references)
            {
                var groupedByDocument = from location in reference.Locations
                                        group location by location.Document into g
                                        select new
                                        {

                                            Document = g.Key,
                                            Locations = g
                                        };

                foreach (var g in groupedByDocument)
                {
                    //var dc = documentChanges.FirstOrDefault(i => Equals(g.Document.Id, i.Document.Id));

                    //if (dc == null)
                    //{
                    //    dc = new DocumentChange();
                    //    dc.Document = sln.GetDocument(g.Document.Id);
                    //    dc.Model = await dc.Document.GetSemanticModelAsync(ct);
                    //    dc.Root = await dc.Model.SyntaxTree.GetRootAsync(ct);
                    //    documentChanges.Add(dc);
                    //}

                    var document = sln.GetDocument(g.Document.Id);
                    var model = await document.GetSemanticModelAsync(ct);
                    var root = await model.SyntaxTree.GetRootAsync(ct);

                    foreach (var location in g.Locations)
                    {
                        var node = root.FindNode(location.Location.SourceSpan);

                        var invocationNode = node.AncestorsAndSelf().FirstOrDefault(n => n.IsKind(SyntaxKind.InvocationExpression)) as InvocationExpressionSyntax;

                        var replacementNode = methodBody;

                        var args = invocationNode.ArgumentList.Arguments;
                        for (var paramIdx = 0; paramIdx < methodParams.Count(); paramIdx++)
                        {
                            var param = methodParams[paramIdx];
                            var arg = (paramIdx < args.Count) ? args[paramIdx].Expression : param.Default.Value;

                            var ids = replacementNode.DescendantNodesAndSelf()
                                .Where(n => n.IsKind(SyntaxKind.IdentifierName))
                                .Where(n => (n as IdentifierNameSyntax).Identifier.ValueText == param.Identifier.ValueText);

                            replacementNode = replacementNode.ReplaceNodes(ids, (n1, n2) => arg).WithAdditionalAnnotations(Formatter.Annotation);
                        }

                        root = root.ReplaceNode(invocationNode, replacementNode.WithAdditionalAnnotations(Formatter.Annotation));
                    }

                    sln = sln.WithDocumentSyntaxRoot(document.Id, root);
                }
            }

            foreach (var dc in documentChanges)
            {
                sln = sln.WithDocumentSyntaxRoot(dc.Document.Id, dc.Root);
            }

            methodSymbol = SymbolFinder.FindSourceDeclarationsAsync(sln, methodSymbol.Name, true, ct).Result.Single();
            references = SymbolFinder.FindReferencesAsync(methodSymbol, sln).Result;

            foreach (var reference in references)
            {
                foreach (var declaringSyntaxReference in reference.Definition.DeclaringSyntaxReferences)
                {
                    var document = sln.GetDocument(declaringSyntaxReference.SyntaxTree);
                    var model = document.GetSemanticModelAsync().Result;
                    var root = model.SyntaxTree.GetRootAsync().Result;
                    var node = root.FindNode(declaringSyntaxReference.Span);
                    root = root.RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia);
                    sln = sln.WithDocumentSyntaxRoot(document.Id, root);
                }
            }

            return await Task.FromResult(sln);
        }

        class DocumentChange
        {
            public Document Document { get; set; }
            public SemanticModel Model { get; set; }
            public SyntaxNode Root { get; set; }

        }

        void ProcessInvocationExpression()
        {

        }

    }
}
