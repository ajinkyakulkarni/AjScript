﻿namespace AjScript.Tests.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AjScript.Interpreter;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LexerTests
    {
        [TestMethod]
        public void ParseName()
        {
            ParseToken("foo", TokenType.Name, "foo");
        }

        [TestMethod]
        public void ParseInteger()
        {
            ParseToken("123", TokenType.Integer, "123");
        }

        [TestMethod]
        public void ParseIntegerWithSpaces()
        {
            ParseToken("  123  ", TokenType.Integer, "123");
        }

        [TestMethod]
        public void ParseIntegerWithDotAndName()
        {
            Lexer lexer = new Lexer("42.foo");

            var token = lexer.NextToken();

            Assert.IsNotNull(token);
            Assert.AreEqual(TokenType.Integer, token.TokenType);
            Assert.AreEqual("42", token.Value);

            token = lexer.NextToken();

            Assert.IsNotNull(token);
            Assert.AreEqual(".", token.Value);
            Assert.AreEqual(TokenType.Delimiter, token.TokenType);

            token = lexer.NextToken();

            Assert.IsNotNull(token);
            Assert.AreEqual(TokenType.Name, token.TokenType);
            Assert.AreEqual("foo", token.Value);

            Assert.IsNull(lexer.NextToken());
        }

        [TestMethod]
        public void ParseNegativeInteger()
        {
            Lexer lexer = new Lexer("-42");

            var token = lexer.NextToken();

            Assert.IsNotNull(token);
            Assert.AreEqual(TokenType.Operator, token.TokenType);
            Assert.AreEqual("-", token.Value);

            token = lexer.NextToken();

            Assert.IsNotNull(token);
            Assert.AreEqual("42", token.Value);
            Assert.AreEqual(TokenType.Integer, token.TokenType);

            Assert.IsNull(lexer.NextToken());
        }

        [TestMethod]
        public void ParseReal()
        {
            ParseToken("123.34", TokenType.Real, "123.34");
        }

        [TestMethod]
        public void ParseRealWithSpaces()
        {
            ParseToken("  123.34 ", TokenType.Real, "123.34");
        }

        [TestMethod]
        public void ParseString()
        {
            ParseToken("\"foo\"", TokenType.String, "foo");
        }

        [TestMethod]
        public void ParseQuotedString()
        {
            ParseToken("'foo\\bar'", TokenType.String, "foo\\bar");
        }

        [TestMethod]
        public void ParseStringWithEscapedChar()
        {
            ParseToken("\"foo\\\"bar\"", TokenType.String, "foo\"bar");
        }

        [TestMethod]
        public void ParseStringWithEscapedChars()
        {
            ParseToken("\"foo\\\"\\r\\n\\t\\f\\b\\a\\vbar\"", TokenType.String, "foo\"\r\n\t\f\b\a\vbar");
        }

        [TestMethod]
        public void ParseSeparators()
        {
            ParseTokens("( ) [ ] ; , :", TokenType.Delimiter);
        }

        [TestMethod]
        public void ParseOperators()
        {
            ParseTokens("= + - * / % ++ -- <= < >= == > != === !==", TokenType.Operator);
        }

        [TestMethod]
        public void ParseWithMultiLineComment()
        {
            ParseNameTokens("foo /* this is a comment\r\n ending here */ bar", "foo", "bar");
        }

        [TestMethod]
        public void ParseWithMultiLinesComment()
        {
            ParseNameTokens("foo /* this is a comment\r\n ending\r\n here ***/ bar", "foo", "bar");
        }

        [TestMethod]
        public void ParseWithLineComment()
        {
            ParseNameTokens("foo // this is a comment\r\n bar", "foo", "bar");
        }

        [TestMethod]
        public void PushToken()
        {
            Lexer lexer = new Lexer(string.Empty);
            Token token = new Token() { TokenType = TokenType.Name, Value = "name" };

            lexer.PushToken(token);

            Assert.AreEqual(token, lexer.NextToken());
        }

        [TestMethod]
        public void PushTwoToken2()
        {
            Lexer lexer = new Lexer(string.Empty);
            Token token = new Token() { TokenType = TokenType.Name, Value = "name" };
            Token token2 = new Token() { TokenType = TokenType.Name, Value = "name2" };

            lexer.PushToken(token);
            lexer.PushToken(token2);

            Assert.AreEqual(token2, lexer.NextToken());
            Assert.AreEqual(token, lexer.NextToken());
        }

        private static void ParseToken(string text, TokenType type, string value)
        {
            using (Lexer lexer = new Lexer(text))
            {
                Token token = lexer.NextToken();

                Assert.IsNotNull(token);

                Assert.AreEqual(type, token.TokenType);
                Assert.AreEqual(value, token.Value);

                Assert.IsNull(lexer.NextToken());
            }
        }

        private static void ParseTokens(string text, TokenType type)
        {
            Lexer lexer = new Lexer(text);
            string[] values = text.Split(' ');
            Token token;

            foreach (string value in values)
            {
                token = lexer.NextToken();

                Assert.IsNotNull(token);

                Assert.AreEqual(type, token.TokenType);
                Assert.AreEqual(value, token.Value);
            }

            Assert.IsNull(lexer.NextToken());
        }

        private static void ParseNameTokens(string text, params string[] values)
        {
            Lexer lexer = new Lexer(text);
            Token token;

            foreach (string value in values)
            {
                token = lexer.NextToken();

                Assert.IsNotNull(token);

                Assert.AreEqual(TokenType.Name, token.TokenType);
                Assert.AreEqual(value, token.Value);
            }

            Assert.IsNull(lexer.NextToken());
        }
    }
}
