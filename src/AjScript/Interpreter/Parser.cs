﻿namespace AjScript.Interpreter
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    using AjScript.Commands;
    using AjScript.Expressions;
    using AjScript.Language;

    public class Parser : IDisposable
    {
        private static readonly Token tokenSemiColon = new Token() { TokenType = TokenType.Delimiter, Value = ";" };
        private static readonly string[] reserved = new string[] { "this", "null", "true", "false", "undefined" };
        private Lexer lexer;
        private IList<ICommand> hoistedCommands = null;

        public Parser(string text)
            : this(new Lexer(text))
        {
        }

        public Parser(TextReader reader)
            : this(new Lexer(reader))
        {
        }

        public Parser(Lexer lexer)
        {
            this.lexer = lexer;
        }

        public ICommand ParseCommands()
        {
            IList<ICommand> commands = new List<ICommand>();
            IList<ICommand> currentHoistedCommands = this.hoistedCommands;

            try
            {
                this.hoistedCommands = new List<ICommand>();

                for (ICommand cmd = this.ParseCommand(); cmd != null; cmd = this.ParseCommand())
                    this.AddCommand(commands, cmd);

                return new CompositeCommand(this.hoistedCommands, commands);
            }
            finally
            {
                this.hoistedCommands = currentHoistedCommands;
            }
        }

        public ICommand ParseCommand()
        {
            Token token = this.lexer.NextToken();

            if (token == null)
                return null;

            if (token.TokenType == TokenType.Name)
            {
                if (token.Value == "if")
                    return this.ParseIfCommand();

                if (token.Value == "while")
                    return this.ParseWhileCommand();

                if (token.Value == "for")
                    return this.ParseForCommand();

                if (token.Value == "return")
                    return this.ParseReturnCommand();

                if (token.Value == "var")
                    return this.ParseVarCommand();

                if (token.Value == "delete")
                    return this.ParseDeleteCommand();
            }

            if (token.TokenType == TokenType.Delimiter && token.Value == "{")
                return this.ParseCompositeCommand();

            this.lexer.PushToken(token);

            ICommand command = this.ParseSimpleCommand();

            if (command == null)
                throw new ParserException(string.Format("Unexpected '{0}'", token.Value));

            this.Parse(TokenType.Delimiter, ";");

            return command;
        }

        public IExpression ParseExpression()
        {
            var expr = this.ParseBinaryLogicalExpressionLevelOne();

            if (expr == null)
                return null;

            if (this.TryParse(TokenType.Name, "instanceof"))
            {
                this.lexer.NextToken();
                return new InstanceOfExpression(expr, this.ParseExpression());
            }

            return expr;
        }

        public void Dispose()
        {
            if (this.lexer != null)
                this.lexer.Dispose();
        }

        private static bool IsNoOperationCommand(ICommand command)
        {
            if (command == null)
                return true;

            if (command is CompositeCommand)
            {
                CompositeCommand composite = (CompositeCommand)command;
                if (composite.CommandCount == 0)
                    return true;
            }

            if (command == NoOperationCommand.Instance)
                return true;

            return false;
        }

        private static bool IsHoistedCommand(ICommand command)
        {
            if (command == null)
                return false;
            if (command is VarCommand)
                return true;
            if (command is ExpressionCommand && ((ExpressionCommand)command).Expression is FunctionExpression
                && ((FunctionExpression)((ExpressionCommand)command).Expression).Name != null)
                return true;
            return false;
        }

        private ICommand ParseSimpleCommand()
        {
            if (this.TryParse(TokenType.Name, "var"))
            {
                this.lexer.NextToken();
                return this.ParseVarCommand();
            }

            IExpression expression = this.ParseExpression();

            if (expression == null)
                return null;

            if (this.TryParse(TokenType.Operator, "="))
            {
                this.lexer.NextToken();

                ICommand command = null;

                if (expression is IndexedExpression)
                {
                    IndexedExpression aexpr = (IndexedExpression)expression;
                    command = new SetArrayCommand(aexpr.Expression, aexpr.Arguments, this.ParseExpression());
                }
                else
                {
                    if (expression is VariableExpression)
                        this.IsValidName(((VariableExpression)expression).Name);

                    command = new SetCommand(expression, this.ParseExpression());
                }

                return command;
            }

            // TODO Review trick to suppor function name(pars) {} without ending ;
            if (expression is FunctionExpression && ((FunctionExpression)expression).Name != null)
                this.lexer.PushToken(tokenSemiColon);

            return new ExpressionCommand(expression);
        }

        private IExpression ParseBinaryLogicalExpressionLevelOne()
        {
            IExpression expression = this.ParseBinaryLogicalExpressionLevelTwo();

            if (expression == null)
                return null;

            while (this.TryParse(TokenType.Operator, "||"))
            {
                this.lexer.NextToken();
                IExpression right = this.ParseBinaryLogicalExpressionLevelTwo();

                expression = new OrExpression(expression, right);
            }

            return expression;
        }

        private IExpression ParseBinaryLogicalExpressionLevelTwo()
        {
            IExpression expression = this.ParseBinaryExpressionZerothLevel();

            if (expression == null)
                return null;

            while (this.TryParse(TokenType.Operator, "&&"))
            {
                this.lexer.NextToken();
                IExpression right = this.ParseBinaryExpressionZerothLevel();

                expression = new AndExpression(expression, right);
            }

            return expression;
        }

        private IExpression ParseBinaryExpressionZerothLevel()
        {
            IExpression expression = this.ParseBinaryExpressionFirstLevel();

            if (expression == null)
                return null;

            while (this.TryParse(TokenType.Operator, "<", ">", "==", ">=", "<=", "!=", "===", "!=="))
            {
                Token oper = this.lexer.NextToken();
                IExpression right = this.ParseBinaryExpressionFirstLevel();

                ComparisonOperator op;

                if (oper.Value == "<")
                    op = ComparisonOperator.Less;
                else if (oper.Value == ">")
                    op = ComparisonOperator.Greater;
                else if (oper.Value == "<=")
                    op = ComparisonOperator.LessEqual;
                else if (oper.Value == ">=")
                    op = ComparisonOperator.GreaterEqual;
                else if (oper.Value == "===")
                    op = ComparisonOperator.Equal;
                else if (oper.Value == "!==")
                    op = ComparisonOperator.NotEqual;
                else if (oper.Value == "==")
                    op = ComparisonOperator.NonStrictEqual;
                else if (oper.Value == "!=")
                    op = ComparisonOperator.NonStrictNotEqual;
                else
                    throw new ParserException(string.Format("Unknown operator '{0}'", oper.Value));

                expression = new CompareExpression(op, expression, right);
            }

            return expression;
        }

        private IExpression ParseBinaryExpressionFirstLevel()
        {
            IExpression expression = this.ParseBinaryExpressionSecondLevel();

            if (expression == null)
                return null;

            while (this.TryParse(TokenType.Operator, "+", "-"))
            {
                Token oper = this.lexer.NextToken();
                IExpression right = this.ParseBinaryExpressionSecondLevel();
                ArithmeticOperator op = oper.Value == "+" ? ArithmeticOperator.Add : ArithmeticOperator.Subtract;

                expression = new ArithmeticBinaryExpression(op, expression, right);
            }

            return expression;
        }

        private IExpression ParseBinaryExpressionSecondLevel()
        {
            IExpression expression = this.ParseUnaryExpression();

            if (expression == null)
                return null;

            while (this.TryParse(TokenType.Operator, "*", "/", @"\", "%"))
            {
                Token oper = this.lexer.NextToken();
                IExpression right = this.ParseUnaryExpression();
                ArithmeticOperator op;

                if (oper.Value == "*")
                    op = ArithmeticOperator.Multiply;
                else if (oper.Value == "/")
                    op = ArithmeticOperator.Divide;
                else if (oper.Value == "\\")
                    op = ArithmeticOperator.IntegerDivide;
                else if (oper.Value == "%")
                    op = ArithmeticOperator.Modulo;
                else
                    throw new ParserException(string.Format("Invalid operator '{0}'", oper.Value));

                expression = new ArithmeticBinaryExpression(op, expression, right);
            }

            return expression;
        }

        private IExpression ParseUnaryExpression()
        {
            if (this.TryParse(TokenType.Operator, "+", "-", "!"))
            {
                Token oper = this.lexer.NextToken();

                IExpression unaryExpression = this.ParseUnaryExpression();

                if (oper.Value == "!")
                    return new NotExpression(unaryExpression);

                ArithmeticOperator op = oper.Value == "+" ? ArithmeticOperator.Plus : ArithmeticOperator.Minus;

                return new ArithmeticUnaryExpression(op, unaryExpression);
            }

            if (this.TryParse(TokenType.Operator, "++", "--"))
            {
                Token oper = this.lexer.NextToken();

                IExpression expression = this.ParseTermExpression();

                IncrementOperator op = oper.Value == "++" ? IncrementOperator.PreIncrement : IncrementOperator.PreDecrement;

                return new IncrementExpression(expression, op);
            }

            IExpression termexpr = this.ParseTermExpression();

            if (this.TryParse(TokenType.Operator, "++", "--"))
            {
                Token oper = this.lexer.NextToken();

                IncrementOperator op = oper.Value == "++" ? IncrementOperator.PostIncrement : IncrementOperator.PostDecrement;

                return new IncrementExpression(termexpr, op);
            }

            return termexpr;
        }

        private IExpression ParseTermExpression()
        {
            if (this.TryParse(TokenType.Name, "new"))
                return this.ParseNewExpression();

            IExpression expression = this.ParseSimpleTermExpression();

            while (this.TryParse(TokenType.Delimiter, ".", "[", "("))
            {
                if (this.TryParse(TokenType.Delimiter, "."))
                {
                    this.lexer.NextToken();
                    string name = this.ParseName();
                    IList<IExpression> arguments = null;

                    if (this.TryParse(TokenType.Delimiter, "("))
                        arguments = this.ParseArgumentList();

                    expression = new DotExpression(expression, name, arguments);
                    continue;
                }

                if (this.TryParse(TokenType.Delimiter, "("))
                {
                    IList<IExpression> arguments = this.ParseArgumentList();
                    expression = new InvokeExpression(expression, arguments);
                    continue;
                }

                expression = new IndexedExpression(expression, this.ParseArrayArgumentList());
            }

            return expression;
        }

        private IExpression ParseNewExpression()
        {
            this.Parse(TokenType.Name, "new");

            IExpression name = this.ParseQualifiedName();
            IList<IExpression> arguments = this.ParseArgumentList();
            return new NewExpression(name, arguments);
        }

        private FunctionExpression ParseFunctionExpression()
        {
            string name = null;
            
            if (this.TryPeekName())
            {
                Token token = this.lexer.NextToken();
                name = token.Value;
            }

            IList<ICommand> currentHoistedCommands = this.hoistedCommands;
            this.hoistedCommands = new List<ICommand>();

            try
            {
                IList<string> arguments = this.ParseArgumentNames();
                this.Parse(TokenType.Delimiter, "{");
                CompositeCommand command = this.ParseCompositeCommand();
                
                // TODO Review, should be 0
                if (command.HoistedCommandCount > 0)
                    throw new ParserException("Invalid command");
                command = new CompositeCommand(this.hoistedCommands, command.Commands);
                return new FunctionExpression(name, arguments.ToArray(), command);
            }
            finally
            {
                this.hoistedCommands = currentHoistedCommands;
            }
        }

        private IList<IExpression> ParseArgumentList()
        {
            List<IExpression> expressions = new List<IExpression>();

            this.Parse(TokenType.Delimiter, "(");

            while (!this.TryParse(TokenType.Delimiter, ")"))
            {
                if (expressions.Count > 0)
                    this.Parse(TokenType.Delimiter, ",");

                expressions.Add(this.ParseExpression());
            }

            System.Diagnostics.Trace.WriteLine("ArgumentList");
            this.Parse(TokenType.Delimiter, ")");

            return expressions;
        }

        private IExpression ParseArrayExpression()
        {
            List<IExpression> expressions = new List<IExpression>();

            while (!this.TryParse(TokenType.Delimiter, "]"))
            {
                if (expressions.Count > 0)
                    this.Parse(TokenType.Delimiter, ",");

                expressions.Add(this.ParseExpression());
            }

            this.Parse(TokenType.Delimiter, "]");

            return new ArrayExpression(expressions);
        }

        private IList<string> ParseArgumentNames()
        {
            List<string> names = new List<string>();

            this.Parse(TokenType.Delimiter, "(");

            while (this.TryPeekName())
            {
                string name = this.ParseName();
                names.Add(name);

                if (this.TryParse(TokenType.Delimiter, ")"))
                    break;

                this.Parse(TokenType.Delimiter, ",");
            }

            this.Parse(TokenType.Delimiter, ")");

            return names;
        }

        private IList<IExpression> ParseArrayArgumentList()
        {
            List<IExpression> expressions = new List<IExpression>();

            this.Parse(TokenType.Delimiter, "[");

            while (!this.TryParse(TokenType.Delimiter, "]"))
            {
                if (expressions.Count > 0)
                    this.Parse(TokenType.Delimiter, ",");

                expressions.Add(this.ParseExpression());
            }

            this.Parse(TokenType.Delimiter, "]");

            return expressions;
        }

        private IExpression ParseSimpleTermExpression()
        {
            Token token = this.lexer.NextToken();

            if (token == null)
                return null;

            if (token.TokenType == TokenType.Name && token.Value == "function")
                return this.ParseFunctionExpression();

            if (token.TokenType == TokenType.Name && token.Value == "typeof")
                return new TypeOfExpression(this.ParseExpression());

            switch (token.TokenType)
            {
                case TokenType.Delimiter:
                    if (token.Value == "(")
                    {
                        IExpression expression = this.ParseExpression();
                        this.Parse(TokenType.Delimiter, ")");
                        return expression;
                    }

                    if (token.Value == "{")
                        return this.ParseObjectExpression();

                    if (token.Value == "[")
                        return this.ParseArrayExpression();

                    break;
                case TokenType.Integer:
                    int intValue = Int32.Parse(token.Value, System.Globalization.CultureInfo.InvariantCulture);
                    return new ConstantExpression(intValue);
                case TokenType.Real:
                    double realValue = Double.Parse(token.Value, System.Globalization.CultureInfo.InvariantCulture);
                    return new ConstantExpression(realValue);
                case TokenType.String:
                    return new ConstantExpression(token.Value);
                case TokenType.Name:
                    if (token.Value == "null")
                        return new ConstantExpression(null);
                    if (token.Value == "undefined")
                        return new ConstantExpression(Undefined.Instance);
                    if (token.Value == "true")
                        return new ConstantExpression(true);
                    if (token.Value == "false")
                        return new ConstantExpression(false);

                    IExpression expr = null;

                    expr = new VariableExpression(token.Value);

                    if (this.TryParse(TokenType.Delimiter, "("))
                    {
                        IList<IExpression> arguments = this.ParseArgumentList();
                        expr = new InvokeExpression(expr, arguments);
                    }

                    return expr;
            }

            throw new ParserException(string.Format("Unexpected '{0}'", token.Value));
        }

        private IExpression ParseObjectExpression()
        {
            IList<string> names = new List<string>();
            IList<IExpression> expressions = new List<IExpression>();

            while (!this.TryParse(TokenType.Delimiter, "}"))
            {
                if (names.Count > 0)
                    this.Parse(TokenType.Delimiter, ",");

                string name = this.ParseName();
                this.Parse(TokenType.Delimiter, ":");
                IExpression expression = this.ParseExpression();
                names.Add(name);
                expressions.Add(expression);
            }

            this.Parse(TokenType.Delimiter, "}");

            return new ObjectExpression(names, expressions);
        }

        private CompositeCommand ParseCompositeCommand()
        {
            IList<ICommand> commands = new List<ICommand>();

            while (!this.TryParse(TokenType.Delimiter, "}"))
                this.AddCommand(commands, this.ParseCommand());

            this.lexer.NextToken();

            return new CompositeCommand(commands);
        }

        private void AddCommand(IList<ICommand> commands, ICommand command)
        {
            if (this.hoistedCommands != null && IsHoistedCommand(command))
                this.hoistedCommands.Add(command);
            else if (!IsNoOperationCommand(command))
                commands.Add(command);
        }

        private ICommand ParseReturnCommand()
        {
            if (this.TryParse(TokenType.Delimiter, ";"))
            {
                this.lexer.NextToken();
                return new ReturnCommand();
            }

            IExpression expression = this.ParseExpression();

            this.Parse(TokenType.Delimiter, ";");

            return new ReturnCommand(expression);
        }

        private ICommand ParseDeleteCommand()
        {
            IExpression expression = this.ParseExpression();

            this.Parse(TokenType.Delimiter, ";");

            return new DeleteCommand(expression);
        }

        private ICommand ParseIfCommand()
        {
            this.Parse(TokenType.Delimiter, "(");
            IExpression condition = this.ParseExpression();
            this.Parse(TokenType.Delimiter, ")");
            ICommand thencmd = this.ParseCommand();

            if (!this.TryParse(TokenType.Name, "else"))
                return new IfCommand(condition, thencmd);

            this.lexer.NextToken();

            ICommand elsecmd = this.ParseCommand();

            return new IfCommand(condition, thencmd, elsecmd);
        }

        private ICommand ParseWhileCommand()
        {
            this.Parse(TokenType.Delimiter, "(");
            IExpression condition = this.ParseExpression();
            this.Parse(TokenType.Delimiter, ")");
            ICommand command = this.ParseCommand();

            return new WhileCommand(condition, command);
        }

        private ICommand ParseForInCommand()
        {
            string name = this.ParseName();
            bool isvar = false;

            if (name == "var")
            {
                name = this.ParseName();
                isvar = true;
            }

            this.Parse(TokenType.Name, "in");
            IExpression values = this.ParseExpression();
            this.Parse(TokenType.Delimiter, ")");
            ICommand command = this.ParseCommand();

            ICommand forcmd = new ForEachCommand(name, values, command);

            if (!isvar)
                return forcmd;

            // TODO review if var command should be hoisted
            ICommand cmds = new CompositeCommand(new List<ICommand>() { new VarCommand(name), forcmd });

            return cmds;
        }

        private ICommand ParseForCommand()
        {
            this.Parse(TokenType.Delimiter, "(");

            Token token = this.lexer.NextToken();

            if (token.TokenType == TokenType.Name && token.Value == "var")
            {
                string name = this.ParseName();

                if (this.TryParse(TokenType.Name, "in"))
                {
                    this.lexer.PushToken(new Token() { TokenType = TokenType.Name, Value = name });
                    this.lexer.PushToken(token);

                    return this.ParseForInCommand();
                }

                this.lexer.PushToken(new Token() { TokenType = TokenType.Name, Value = name });
                this.lexer.PushToken(token);
            }

            ICommand initial = this.ParseSimpleCommand();

            // this.Parse(TokenType.Separator, ";");
            IExpression condition = this.ParseExpression();
            this.Parse(TokenType.Delimiter, ";");
            ICommand endcmd = this.ParseSimpleCommand();
            this.Parse(TokenType.Delimiter, ")");
            ICommand command = this.ParseCommand();

            return new ForCommand(initial, condition, endcmd, command);
        }

        private ICommand ParseVarCommand()
        {
            string name = this.ParseName();

            this.IsValidName(name);

            IExpression expression = null;

            if (this.TryParse(TokenType.Operator, "="))
            {
                this.lexer.NextToken();
                expression = this.ParseExpression();
            }

            this.Parse(TokenType.Delimiter, ";");

            if (this.hoistedCommands != null)
            {
                this.hoistedCommands.Add(new VarCommand(name));
                if (expression == null)
                    return NoOperationCommand.Instance;
                return new SetVariableCommand(name, expression);
            }

            if (expression == null)
                return new VarCommand(name);

            return new CompositeCommand(new List<ICommand>() { new VarCommand(name), new SetVariableCommand(name, expression) });
        }

        private bool TryPeekName()
        {
            Token token = this.lexer.PeekToken();

            if (token == null)
                return false;

            return token.TokenType == TokenType.Name;
        }

        private bool TryParse(TokenType type, params string[] values)
        {
            Token token = this.lexer.PeekToken();

            if (token == null)
                return false;

            if (token.TokenType == type)
                foreach (string value in values)
                    if (token.Value == value)
                        return true;

            return false;
        }

        private void Parse(TokenType type, string value)
        {
            Token token = this.lexer.NextToken();

            if (token == null)
                throw new ParserException(string.Format("Expected '{0}'", value));

            if (token.TokenType != type || token.Value != value)
                throw new ParserException(string.Format("Expected '{0}'", value));
        }

        private string ParseName()
        {
            Token token = this.lexer.NextToken();

            if (token == null)
                throw new ParserException("Expected a name");

            if (token.TokenType == TokenType.Name)
                return token.Value;

            throw new ParserException("Expected a name");
        }

        private IExpression ParseQualifiedName()
        {
            string name = this.ParseName();
            IExpression expression = new VariableExpression(name);

            while (this.TryParse(TokenType.Delimiter, "."))
            {
                this.lexer.NextToken();
                expression = new DotExpression(expression, this.ParseName());
            }

            return expression;
        }

        private void IsValidName(string name)
        {
            if (reserved.Contains(name))
                throw new ParserException(string.Format("Invalid name '{0}'", name));
        }
    }
}
