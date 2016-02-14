﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSS = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactoringEssentials.VB.Converter
{
    public partial class CSharpConverter
    {
        class MethodBodyVisitor : CS.CSharpSyntaxVisitor<SyntaxList<StatementSyntax>>
        {
            SemanticModel semanticModel;
            NodesVisitor nodesVisitor;

            public MethodBodyVisitor(SemanticModel semanticModel, NodesVisitor nodesVisitor)
            {
                this.semanticModel = semanticModel;
                this.nodesVisitor = nodesVisitor;
            }

            public override SyntaxList<StatementSyntax> DefaultVisit(SyntaxNode node)
            {
                throw new NotImplementedException(node.GetType() + " not implemented!");
            }

            public override SyntaxList<StatementSyntax> VisitLocalDeclarationStatement(CSS.LocalDeclarationStatementSyntax node)
            {
                var modifiers = ConvertModifiers(node.Modifiers, TokenContext.Local);
                if (modifiers.Count == 0)
                    modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.DimKeyword));
                return SyntaxFactory.SingletonList<StatementSyntax>(
                    SyntaxFactory.LocalDeclarationStatement(
                        modifiers,
                        RemodelVariableDeclaration(node.Declaration, nodesVisitor)
                    )
                );
            }

            public override SyntaxList<StatementSyntax> VisitExpressionStatement(CSS.ExpressionStatementSyntax node)
            {
                var exprNode = node.Expression.Accept(nodesVisitor);
                if (!(exprNode is StatementSyntax))
                    exprNode = SyntaxFactory.ExpressionStatement((ExpressionSyntax)exprNode);

                return SyntaxFactory.SingletonList((StatementSyntax)exprNode);
            }

            public override SyntaxList<StatementSyntax> VisitIfStatement(CSS.IfStatementSyntax node)
            {
                StatementSyntax stmt;
                var elseIfBlocks = new List<ElseIfBlockSyntax>();
                ElseBlockSyntax elseBlock = null;
                CollectElseBlocks(node, elseIfBlocks, ref elseBlock);
                if (node.Statement is CSS.BlockSyntax)
                {
                    var b = (CSS.BlockSyntax)node.Statement;
                    stmt = SyntaxFactory.MultiLineIfBlock(
                        SyntaxFactory.IfStatement((ExpressionSyntax)node.Condition.Accept(nodesVisitor)).WithThenKeyword(SyntaxFactory.Token(SyntaxKind.ThenKeyword)),
                        SyntaxFactory.List(b.Statements.SelectMany(s => s.Accept(this))),
                        SyntaxFactory.List(elseIfBlocks),
                        elseBlock
                    );
                }
                else
                {
                    if (elseIfBlocks.Any())
                    {
                        stmt = SyntaxFactory.MultiLineIfBlock(
                             SyntaxFactory.IfStatement((ExpressionSyntax)node.Condition.Accept(nodesVisitor)).WithThenKeyword(SyntaxFactory.Token(SyntaxKind.ThenKeyword)),
                             node.Statement.Accept(this),
                             SyntaxFactory.List(elseIfBlocks),
                             elseBlock
                         );
                    }
                    else
                    {
                        stmt = SyntaxFactory.SingleLineIfStatement(
                            (ExpressionSyntax)node.Condition.Accept(nodesVisitor),
                            node.Statement.Accept(this),
                            elseBlock == null ? null : SyntaxFactory.SingleLineElseClause(elseBlock.Statements)
                        ).WithThenKeyword(SyntaxFactory.Token(SyntaxKind.ThenKeyword));
                    }
                }
                return SyntaxFactory.SingletonList<StatementSyntax>(stmt);
            }

            void CollectElseBlocks(CSS.IfStatementSyntax node, List<ElseIfBlockSyntax> elseIfBlocks, ref ElseBlockSyntax elseBlock)
            {
                if (node.Else == null) return;
                if (node.Else.Statement is CSS.IfStatementSyntax)
                {
                    var elseIf = (CSS.IfStatementSyntax)node.Else.Statement;
                    if (elseIf.Statement is CSS.BlockSyntax)
                    {
                        var block = (CSS.BlockSyntax)elseIf.Statement;
                        elseIfBlocks.Add(
                            SyntaxFactory.ElseIfBlock(
                                SyntaxFactory.ElseIfStatement((ExpressionSyntax)elseIf.Condition.Accept(nodesVisitor)).WithThenKeyword(SyntaxFactory.Token(SyntaxKind.ThenKeyword)),
                                SyntaxFactory.List(block.Statements.SelectMany(s => s.Accept(this)))
                            )
                        );
                    }
                    else
                    {
                        elseIfBlocks.Add(
                            SyntaxFactory.ElseIfBlock(
                                SyntaxFactory.ElseIfStatement((ExpressionSyntax)elseIf.Condition.Accept(nodesVisitor)).WithThenKeyword(SyntaxFactory.Token(SyntaxKind.ThenKeyword)),
                                elseIf.Statement.Accept(this)
                            )
                        );
                    }
                    CollectElseBlocks(elseIf, elseIfBlocks, ref elseBlock);
                }
                else if (node.Else.Statement is CSS.BlockSyntax)
                {
                    var block = (CSS.BlockSyntax)node.Else.Statement;
                    elseBlock = SyntaxFactory.ElseBlock(SyntaxFactory.List(block.Statements.SelectMany(s => s.Accept(this))));
                }
                else
                {
                    elseBlock = SyntaxFactory.ElseBlock(SyntaxFactory.List(node.Else.Statement.Accept(this)));
                }
            }

            public override SyntaxList<StatementSyntax> VisitDoStatement(CSS.DoStatementSyntax node)
            {
                var condition = (ExpressionSyntax)node.Condition.Accept(nodesVisitor);
                var stmt = ConvertBlock(node.Statement);
                var block = SyntaxFactory.DoLoopWhileBlock(
                    SyntaxFactory.DoStatement(SyntaxKind.SimpleDoStatement),
                    stmt,
                    SyntaxFactory.LoopStatement(SyntaxKind.LoopWhileStatement, SyntaxFactory.WhileClause(condition))
                );

                return SyntaxFactory.SingletonList<StatementSyntax>(block);
            }

            public override SyntaxList<StatementSyntax> VisitWhileStatement(CSS.WhileStatementSyntax node)
            {
                var condition = (ExpressionSyntax)node.Condition.Accept(nodesVisitor);
                var stmt = ConvertBlock(node.Statement);
                var block = SyntaxFactory.WhileBlock(
                    SyntaxFactory.WhileStatement(condition),
                    stmt
                );

                return SyntaxFactory.SingletonList<StatementSyntax>(block);
            }

            public override SyntaxList<StatementSyntax> VisitForEachStatement(CSS.ForEachStatementSyntax node)
            {
                VisualBasicSyntaxNode variable;
                if (node.Type.IsVar)
                {
                    variable = SyntaxFactory.IdentifierName(ConvertIdentifier(node.Identifier));
                }
                else
                {
                    variable = SyntaxFactory.VariableDeclarator(
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.ModifiedIdentifier(ConvertIdentifier(node.Identifier))),
                        SyntaxFactory.SimpleAsClause((TypeSyntax)node.Type.Accept(nodesVisitor)),
                        null
                    );
                }
                var expression = (ExpressionSyntax)node.Expression.Accept(nodesVisitor);
                var stmt = ConvertBlock(node.Statement);
                var block = SyntaxFactory.ForEachBlock(
                    SyntaxFactory.ForEachStatement(variable, expression),
                    stmt,
                    SyntaxFactory.NextStatement()
                );
                return SyntaxFactory.SingletonList<StatementSyntax>(block);
            }

            public override SyntaxList<StatementSyntax> VisitUsingStatement(CSS.UsingStatementSyntax node)
            {
                var stmt = SyntaxFactory.UsingStatement(
                    (ExpressionSyntax)node.Expression?.Accept(nodesVisitor),
                    RemodelVariableDeclaration(node.Declaration, nodesVisitor)
                );
                return SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.UsingBlock(stmt, ConvertBlock(node.Statement)));
            }

            SyntaxList<StatementSyntax> ConvertBlock(CSS.StatementSyntax node)
            {
                if (node is CSS.BlockSyntax)
                {
                    var b = (CSS.BlockSyntax)node;
                    return SyntaxFactory.List(b.Statements.SelectMany(s => s.Accept(this)));
                }
                else
                {
                    return node.Accept(this);
                }
            }

            public override SyntaxList<StatementSyntax> VisitReturnStatement(CSS.ReturnStatementSyntax node)
            {
                StatementSyntax stmt;
                if (node.Expression == null)
                    stmt = SyntaxFactory.ReturnStatement();
                else
                    stmt = SyntaxFactory.ReturnStatement((ExpressionSyntax)node.Expression.Accept(nodesVisitor));
                return SyntaxFactory.SingletonList(stmt);
            }

            public override SyntaxList<StatementSyntax> VisitThrowStatement(CSS.ThrowStatementSyntax node)
            {
                StatementSyntax stmt;
                if (node.Expression == null)
                    stmt = SyntaxFactory.ThrowStatement();
                else
                    stmt = SyntaxFactory.ThrowStatement((ExpressionSyntax)node.Expression.Accept(nodesVisitor));
                return SyntaxFactory.SingletonList(stmt);
            }

            public override SyntaxList<StatementSyntax> VisitContinueStatement(CSS.ContinueStatementSyntax node)
            {
                var statementKind = SyntaxKind.None;
                var keywordKind = SyntaxKind.None;
                foreach (var stmt in node.GetAncestors<CSS.StatementSyntax>())
                {
                    if (stmt is CSS.DoStatementSyntax)
                    {
                        statementKind = SyntaxKind.ContinueDoStatement;
                        keywordKind = SyntaxKind.DoKeyword;
                        break;
                    }
                    if (stmt is CSS.WhileStatementSyntax)
                    {
                        statementKind = SyntaxKind.ContinueWhileStatement;
                        keywordKind = SyntaxKind.WhileKeyword;
                        break;
                    }
                    if (stmt is CSS.ForStatementSyntax || stmt is CSS.ForEachStatementSyntax)
                    {
                        statementKind = SyntaxKind.ContinueForStatement;
                        keywordKind = SyntaxKind.ForKeyword;
                        break;
                    }
                }
                return SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ContinueStatement(statementKind, SyntaxFactory.Token(keywordKind)));
            }

            public override SyntaxList<StatementSyntax> VisitBreakStatement(CSS.BreakStatementSyntax node)
            {
                var statementKind = SyntaxKind.None;
                var keywordKind = SyntaxKind.None;
                foreach (var stmt in node.GetAncestors<CSS.StatementSyntax>())
                {
                    if (stmt is CSS.DoStatementSyntax)
                    {
                        statementKind = SyntaxKind.ExitDoStatement;
                        keywordKind = SyntaxKind.DoKeyword;
                        break;
                    }
                    if (stmt is CSS.WhileStatementSyntax)
                    {
                        statementKind = SyntaxKind.ExitWhileStatement;
                        keywordKind = SyntaxKind.WhileKeyword;
                        break;
                    }
                    if (stmt is CSS.ForStatementSyntax || stmt is CSS.ForEachStatementSyntax)
                    {
                        statementKind = SyntaxKind.ExitForStatement;
                        keywordKind = SyntaxKind.ForKeyword;
                        break;
                    }
                }
                return SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ExitStatement(statementKind, SyntaxFactory.Token(keywordKind)));
            }

            public override SyntaxList<StatementSyntax> VisitCheckedStatement(CSS.CheckedStatementSyntax node)
            {
                return WrapInComment(Visit(node.Block), "Visual Basic does not support checked statements!");
            }

            private SyntaxList<StatementSyntax> WrapInComment(SyntaxList<StatementSyntax> nodes, string comment)
            {
                if (nodes.Count > 0)
                {
                    nodes = nodes.Replace(nodes[0], nodes[0].WithPrependedLeadingTrivia(SyntaxFactory.CommentTrivia("BEGIN TODO : " + comment)));
                    nodes = nodes.Replace(nodes.Last(), nodes.Last().WithAppendedTrailingTrivia(SyntaxFactory.CommentTrivia("END TODO : " + comment)));
                }

                return nodes;
            }
        }
    }
}
