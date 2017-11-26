using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace QuasiRefactoring
{
    class Program
    {
        static void Main(string[] args)
        {
            var solutionPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName, Assembly.GetExecutingAssembly().GetName().Name + ".sln");
            var projectName = "Interface";
            var refactorerer = new Refactorer(solutionPath, projectName);
            var interfaceClassName = "IAppService";

            // load or make aspect from nodes, source code, etc
            var aspect = SyntaxFactory.ParseStatement("Checker.CheckSomething();");

            //refactorerer.AddAspect(interfaceClassName, new List<StatementSyntax>(){aspect});

            projectName = "ObsoleteApi";
            interfaceClassName = "IAppService";
            
            var refactorererApi = new Refactorer(solutionPath, projectName);
            refactorererApi.ReplaceObsoleteApiCalls(interfaceClassName, "Use ");
        }
    }
}
