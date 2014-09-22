﻿namespace AjScript.Tests.Language
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AjScript;
    using AjScript.Commands;
    using AjScript.Expressions;
    using AjScript.Language;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DynamicObjectTests
    {
        private DynamicObject dynobj;
        private Function function;

        [TestInitialize]
        public void SetupDynamicObject()
        {
            this.function = new Function(null, null);
            this.dynobj = new DynamicObject(this.function);
        }

        [TestMethod]
        public void GetUndefinedForUndefinedValue()
        {
            Assert.AreSame(Undefined.Instance, this.dynobj.GetValue("Foo"));
        }

        [TestMethod]
        public void SetAndGetValue()
        {
            this.dynobj.SetValue("Foo", "Bar");

            Assert.AreEqual("Bar", this.dynobj.GetValue("Foo"));
        }

        [TestMethod]
        public void GetNames()
        {
            this.dynobj.SetValue("FirstName", "Adam");
            this.dynobj.SetValue("LastName", "Genesis");

            ICollection<string> names = this.dynobj.GetNames();

            Assert.IsNotNull(names);
            Assert.AreEqual(2, names.Count);

            Assert.IsTrue(names.Contains("FirstName"));
            Assert.IsTrue(names.Contains("LastName"));
        }

        [TestMethod]
        public void DefineMethod()
        {
            ICommand body = new ReturnCommand(new VariableExpression("Name"));
            Function function = new Function(null, body);

            this.dynobj.SetValue("GetName", function);

            object result = this.dynobj.GetValue("GetName");

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ICallable));
            Assert.IsTrue(result == function);
        }

        [TestMethod]
        public void InvokeMethod()
        {
            ICommand body = new ReturnCommand(new DotExpression(new VariableExpression("this"), "Name"));
            Function function = new Function(null, body);

            this.dynobj.SetValue("Name", "Adam");
            this.dynobj.SetValue("GetName", function);

            object result = this.dynobj.Invoke("GetName", new object[] { });

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.AreEqual("Adam", result);
        }

        [TestMethod]
        public void InvokeNativeMethod()
        {
            ICommand body = new ReturnCommand(new VariableExpression("Name"));
            Function function = new Function(null, body);

            this.dynobj.SetValue("Name", "Adam");

            object result = this.dynobj.Invoke("GetValue", new object[] { "Name" });

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.AreEqual("Adam", result);
        }

        [TestMethod]
        public void DetectNativeMethods()
        {
            Assert.IsTrue(this.dynobj.IsNativeMethod("ToString"));
            Assert.IsTrue(this.dynobj.IsNativeMethod("GetHashCode"));
            Assert.IsTrue(this.dynobj.IsNativeMethod("Equals"));

            Assert.IsTrue(this.dynobj.IsNativeMethod("GetValue"));
            Assert.IsTrue(this.dynobj.IsNativeMethod("SetValue"));
            Assert.IsTrue(this.dynobj.IsNativeMethod("GetNames"));
            Assert.IsTrue(this.dynobj.IsNativeMethod("Invoke"));

            Assert.IsFalse(this.dynobj.IsNativeMethod("Foo"));
        }

        [TestMethod]
        public void UsePrototypeForGetValue()
        {
            DynamicObject prototype = new DynamicObject();
            this.function.SetValue("prototype", prototype);
            prototype.SetValue("x", 10);

            Assert.AreEqual(10, this.dynobj.GetValue("x"));
        }

        [TestMethod]
        public void GetValueFromPrototypeAndSetValueInObject()
        {
            DynamicObject prototype = new DynamicObject();
            this.function.SetValue("prototype", prototype);
            prototype.SetValue("x", 10);
            this.dynobj.SetValue("x", 20);
            Assert.AreEqual(20, this.dynobj.GetValue("x"));
            Assert.AreEqual(10, prototype.GetValue("x"));
        }
    }
}
