﻿namespace AjScript
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AjScript.Language;

    public class Context : IContext
    {
        private Dictionary<string, object> values;
        private IContext parent;

        public Context()
            : this(null)
        {
        }

        public Context(IContext parent)
        {
            this.parent = parent;
            this.values = new Dictionary<string, object>();
        }

        public ReturnValue ReturnValue { get; set; }

        public IContext RootContext
        {
            get
            {
                if (this.parent == null)
                    return this;

                return this.parent.RootContext;
            }
        }

        public ICollection<string> GetNames()
        {
            return this.values.Keys;
        }

        public object GetValue(string name)
        {
            if (!this.values.ContainsKey(name))
                if (this.parent != null)
                    return this.parent.GetValue(name);
                else
                    return Undefined.Instance;

            return this.values[name];
        }

        public void SetValue(string name, object value)
        {
            if (this.values.ContainsKey(name) || this.parent == null)
                this.values[name] = value;
            else
                this.parent.SetValue(name, value);
        }

        public void DefineVariable(string name)
        {
            this.values[name] = Undefined.Instance;
        }

        public void RemoveValue(string name)
        {
            if (this.values.ContainsKey(name))
                this.values.Remove(name);
            else if (this.parent != null)
                this.parent.RemoveValue(name);
        }
    }
}
