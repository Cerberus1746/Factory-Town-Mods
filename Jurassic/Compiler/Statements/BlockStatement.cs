using System;
using System.Collections.Generic;

namespace Jurassic.Compiler
{

    /// <summary>
    /// Represents a javascript block statement.
    /// </summary>
    internal class BlockStatement : Statement
    {
        private List<Statement> statements = new List<Statement>();

        /// <summary>
        /// Creates a new BlockStatement instance.
        /// </summary>
        /// <param name="labels"> The labels that are associated with this statement. </param>
        public BlockStatement(IList<string> labels)
            : base(labels)
        {
        }

        /// <summary>
        /// Gets a list of the statements in the block.
        /// </summary>
        public IList<Statement> Statements
        {
            get { return this.statements; }
        }

        /// <summary>
        /// Generates CIL for the statement.
        /// </summary>
        /// <param name="generator"> The generator to output the CIL to. </param>
        /// <param name="optimizationInfo"> Information about any optimizations that should be performed. </param>
        public override void GenerateCode(ILGenerator generator, OptimizationInfo optimizationInfo)
        {
            // Generate code for the start of the statement.
            StatementLocals statementLocals = new StatementLocals() { NonDefaultSourceSpanBehavior = true };
            this.GenerateStartOfStatement(generator, optimizationInfo, statementLocals);

            foreach (Statement statement in this.Statements)
            {
                // Generate code for the statement.
                statement.GenerateCode(generator, optimizationInfo);
            }

            // Generate code for the end of the statement.
            this.GenerateEndOfStatement(generator, optimizationInfo, statementLocals);
        }

        /// <summary>
        /// Gets an enumerable list of child nodes in the abstract syntax tree.
        /// </summary>
        public override IEnumerable<AstNode> ChildNodes
        {
            get
            {
                foreach (Statement statement in this.Statements) {
                    yield return statement;
                }
            }
        }

        /// <summary>
        /// Converts the statement to a string.
        /// </summary>
        /// <param name="indentLevel"> The number of tabs to include before the statement. </param>
        /// <returns> A string representing this statement. </returns>
        public override string ToString(int indentLevel)
        {
            indentLevel = Math.Max(indentLevel - 1, 0);
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            result.Append(new string('\t', indentLevel));
            result.AppendLine("{");
            foreach (Statement statement in this.Statements) {
                result.AppendLine(statement.ToString(indentLevel + 1));
            }

            result.Append(new string('\t', indentLevel));
            result.Append("}");
            return result.ToString();
        }
    }

}