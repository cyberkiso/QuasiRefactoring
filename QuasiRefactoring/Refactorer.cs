using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Editing;

namespace QuasiRefactoring
{
    public class Refactorer
    {
        public Project Project { get; set; }
        public MSBuildWorkspace Workspace { get; set; }
        public Solution Solution { get; set; }

        public Refactorer(string solutionPath, string projectName)
        {
            // start Roslyn workspace
            Workspace = MSBuildWorkspace.Create();

            // open solution we want to analyze
            Solution = Workspace.OpenSolutionAsync(solutionPath).Result;

            Project = Solution.Projects.FirstOrDefault(p => p.Name == projectName);
        }
        #region add aspect stuff
        public void AddAspect(string interfaceClassName, IEnumerable<StatementSyntax> aspect, List<Project> excludedProjects = null, List<string> excludedClasses = null)
        {
            // step 1: get semantic model of interface for getting references
            //var interfaceClassNameWithExt = interfaceClassName + ".cs";
            //var document = Project.Documents.FirstOrDefault(d => d.Name == interfaceClassNameWithExt);
            var document = GetDocument(interfaceClassName);
            var model = document.GetSemanticModelAsync().Result;

            // get methods that need implementing scattered functionality
            var methodDeclarations = GetMethodDeclarations(interfaceClassName, document);

            List<MethodDocument> implMethodDeclarations = new List<MethodDocument>();
            // get implemetations of interface methods
            foreach (var methodDeclarationSyntax in methodDeclarations)
            {
                var methodSymbol = model.GetDeclaredSymbol(methodDeclarationSyntax);
                var usingReferences = SymbolFinder.FindReferencesAsync(methodSymbol, Solution).Result.Where(r => r.Locations.Count() > 0);

                //Add filter by using in 
                if (ContainInProjects(usingReferences, excludedProjects) | ContainInClasses(usingReferences, excludedClasses))
                {
                    continue;
                }

                implMethodDeclarations.AddRange(GetMethodImplementations(methodSymbol));
            }

            //add aspect & update document
            UpdateSolutionWithAction(aspect, implMethodDeclarations, InsertAspect);
        }

        private Document GetDocument(string interfaceClassName)
        {
            var document = Project.Documents
                .FirstOrDefault(d => d.GetSyntaxRootAsync().Result
                    .DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == interfaceClassName) != null);
            return document;
        }

        private void UpdateSolutionWithAction(object actionParameters, List<MethodDocument> implMethodDeclarations, Action<DocumentEditor, MethodDocument, object> action)
        {
            foreach (var impl in implMethodDeclarations)
            {
                var documentEditor = DocumentEditor.CreateAsync(impl.Document).Result;
                action(documentEditor, impl, actionParameters);
                impl.Document = documentEditor.GetChangedDocument();

                Solution = Solution.WithDocumentSyntaxRoot(impl.Document.Id, impl.Document.GetSyntaxRootAsync().Result.NormalizeWhitespace());
            }
            Workspace.TryApplyChanges(Solution);
            UpdateRefactorerEnv();
        }

        private void InsertAspect(DocumentEditor documentEditor, MethodDocument impl, object aspect)
        {
            documentEditor.InsertBefore(impl.Method.Body.ChildNodes().FirstOrDefault(), aspect as IEnumerable<StatementSyntax>);
        }

        private List<MethodDocument> GetMethodImplementations(IMethodSymbol methodSymbol)
        {
            List<MethodDocument> implMethodDeclarations = new List<MethodDocument>();

            var methodRefs = SymbolFinder
                .FindImplementationsAsync(methodSymbol, Solution).Result;
            var definitionRefs = methodRefs.Where(s => s.IsDefinition).Select(r => r.DeclaringSyntaxReferences.FirstOrDefault());
            foreach (var defRef in definitionRefs)
            {
                implMethodDeclarations.Add(
                    new MethodDocument()
                    {
                        Method = defRef.GetSyntax() as MethodDeclarationSyntax,
                        Document = Project.Documents.FirstOrDefault(d => d.GetSyntaxTreeAsync().Result.IsEquivalentTo(defRef.SyntaxTree))
                    });
            }
            return implMethodDeclarations;
        }

        /// <param name="excludedClasses">Exclude method declarations that using in excluded classes in current Solution</param>
        private bool ContainInClasses(IEnumerable<ReferencedSymbol> usingReferences, List<string> excludedClasses)
        {
            if (excludedClasses.Count <= 0)
            {
                return false;
            }

            foreach (var reference in usingReferences)
            {
                foreach (var location in reference.Locations)
                {
                    var node = location.Location.SourceTree.GetRoot().FindNode(location.Location.SourceSpan);
                    ClassDeclarationSyntax classDeclaration = null;
                    if (SyntaxNodeHelper.TryGetParentSyntax(node, out classDeclaration))
                    {
                        if (excludedClasses.Contains(classDeclaration.Identifier.Text))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        static class SyntaxNodeHelper
        {
            public static bool TryGetParentSyntax<T>(SyntaxNode syntaxNode, out T result)
                where T : SyntaxNode
            {
                // set defaults
                result = null;

                if (syntaxNode == null)
                {
                    return false;
                }

                try
                {
                    syntaxNode = syntaxNode.Parent;

                    if (syntaxNode == null)
                    {
                        return false;
                    }

                    if (syntaxNode.GetType() == typeof(T))
                    {
                        result = syntaxNode as T;
                        return true;
                    }

                    return TryGetParentSyntax<T>(syntaxNode, out result);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <param name="excludedProjects">Exclude method declarations that using in excluded projects in current Solution</param>
        private bool ContainInProjects(IEnumerable<ReferencedSymbol> usingReferences, List<Microsoft.CodeAnalysis.Project> excludedProjects)
        {
            if (excludedProjects.Count <= 0)
            {
                return false;
            }
            foreach (var reference in usingReferences)
            {
                if (excludedProjects.FirstOrDefault(p => reference.Locations.FirstOrDefault(l => l.Document.Project.Id == p.Id) != null) != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <param name="interfaceClassName">Interface class name</param>
        /// <param name="document">Interface class document</param>
        private List<MethodDeclarationSyntax> GetMethodDeclarations(string interfaceClassName, Document document)
        {
            //var sourcePath = document.FilePath;
            //string sourceCode = File.ReadAllText(sourcePath);
            //SyntaxTree tree = document.GetSyntaxTreeAsync().Result;//CSharpSyntaxTree.ParseText(sourceCode);

            SyntaxNode root = document.GetSyntaxRootAsync().Result;// tree.GetRoot();
            var targetInterfaceClass =
                root.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == interfaceClassName);
            var methodDeclarations = targetInterfaceClass.DescendantNodes().OfType<MethodDeclarationSyntax>();

            return methodDeclarations.ToList();
        }

        private class MethodDocument
        {
            public MethodDeclarationSyntax Method { get; set; }
            public Document Document { get; set; }
        }
        #endregion

        #region replace obsolete api calls
        public void ReplaceObsoleteApiCalls(string interfaceClassName, string obsoleteMessageSignature)
        {
            var document = GetDocument(interfaceClassName);
            var model = document.GetSemanticModelAsync().Result;
            // find obsolete

            var methodDeclarations = GetMethodDeclarations(interfaceClassName, document);
            //var methodSymbol = model.GetDeclaredSymbol(methodDeclarations.FirstOrDefault());
            //var res = methodSymbol.GetAttributes().FirstOrDefault(a=>a.AttributeClass.Name=="ObsoleteAttribute");

            var attribute = methodDeclarations.FirstOrDefault().AttributeLists.FirstOrDefault().Attributes.FirstOrDefault();
            var name = (attribute.Name as IdentifierNameSyntax).Identifier.Text;//"Obsolete"

            var obsoleteMethods = methodDeclarations
                .Where(m => m.AttributeLists
                    .FirstOrDefault(a => a.Attributes
                        .FirstOrDefault(atr => (atr.Name as IdentifierNameSyntax).Identifier.Text == "Obsolete") != null) != null).ToList();

            List<ObsoleteReplacement> replacementMap = new List<ObsoleteReplacement>();

            // find new for replace
            foreach (var method in obsoleteMethods)
            {
                var methodName = GetMethodName(obsoleteMessageSignature, method);
                if (methodDeclarations.FirstOrDefault(m => m.Identifier.Text == methodName) != null)
                {
                    // find all reference of obsolete call
                    var methodSymbol = model.GetDeclaredSymbol(method);
                    var usingReferences = SymbolFinder.FindReferencesAsync(methodSymbol, Solution).Result.Where(r => r.Locations.Count() > 0);

                    replacementMap.Add(new ObsoleteReplacement() 
                    { 
                        ObsoleteMethod = SyntaxFactory.IdentifierName(method.Identifier.Text),
                        ObsoleteReferences = usingReferences,
                        NewMethod = SyntaxFactory.IdentifierName(methodName) 
                    });
                }
            }
            
            //update identifier obsolete with identifier new method
            UpdateSolutionWithAction(replacementMap, ReplaceMethod);

        }

        private string GetMethodName(string obsoleteMessagePattern, MethodDeclarationSyntax method)
        {
            var message = GetAttributeMessage(method);
            int index = message.LastIndexOf(obsoleteMessagePattern) + obsoleteMessagePattern.Length;
            return message.Substring(index);
        }

        private static string GetAttributeMessage(MethodDeclarationSyntax method)
        {
            var obsoleteAttribute = method.AttributeLists.FirstOrDefault().Attributes.FirstOrDefault(atr => (atr.Name as IdentifierNameSyntax).Identifier.Text == "Obsolete");
            var messageArgument = obsoleteAttribute.ArgumentList.DescendantNodes().OfType<AttributeArgumentSyntax>()
                .FirstOrDefault(arg => arg.ChildNodes().OfType<LiteralExpressionSyntax>().Count() != 0);
            var message = messageArgument.ChildNodes().FirstOrDefault().GetText();
            return message.ToString().Trim('\"');
        }


        private void UpdateSolutionWithAction(List<ObsoleteReplacement> replacementMap, Action<DocumentEditor, ObsoleteReplacement, SyntaxNode> action)
        {
            var workspace = MSBuildWorkspace.Create();
            
            foreach (var item in replacementMap)
            {
                var solution = workspace.OpenSolutionAsync(Solution.FilePath).Result;
                var project = solution.Projects.FirstOrDefault(p => p.Name == Project.Name);
                foreach (var reference in item.ObsoleteReferences)
                {
                    var docs = reference.Locations.Select(l => l.Document);
                    foreach (var doc in docs)
                    {
                        var document = project.Documents.FirstOrDefault(d => d.Name == doc.Name);
                        var documentEditor = DocumentEditor.CreateAsync(document).Result;
                        action(documentEditor, item, document.GetSyntaxRootAsync().Result);
                        document = documentEditor.GetChangedDocument();
                        solution = solution.WithDocumentSyntaxRoot(document.Id, document.GetSyntaxRootAsync().Result.NormalizeWhitespace());
                    }
                }
                var result = workspace.TryApplyChanges(solution);
                workspace.CloseSolution();
            }
            UpdateRefactorerEnv();
        }

        private void ReplaceMethod(DocumentEditor documentEditor, ObsoleteReplacement item, SyntaxNode root)
        {
            var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
            var usingTokens = identifiers.Where(i => i.Identifier.Text == item.ObsoleteMethod.Identifier.Text);
            foreach (var oldMethod in usingTokens)
            {
                documentEditor.ReplaceNode(oldMethod, item.NewMethod);
            }
        }

        private class ObsoleteReplacement
        {
            public IdentifierNameSyntax ObsoleteMethod { get; set; }
            public IEnumerable<ReferencedSymbol> ObsoleteReferences { get; set; }
            public IdentifierNameSyntax NewMethod { get; set; }
        }
        #endregion

        private void UpdateRefactorerEnv()
        {
            Workspace = MSBuildWorkspace.Create();
            Solution = Workspace.OpenSolutionAsync(Solution.FilePath).Result;
            Project = Solution.Projects.FirstOrDefault(p => p.Name == Project.Name);
        }
    }
}
