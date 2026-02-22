using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualBasic;
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// See Compiler::LoadXmlSolutionExtension
namespace PCL.My
{
    [Embedded()]
    [DebuggerNonUserCode()]
    [System.Runtime.CompilerServices.CompilerGenerated()]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal sealed class InternalXmlHelper
    {
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        private InternalXmlHelper()
        {
        }
        public static string get_Value(IEnumerable<XElement> source)
        {
            foreach (XElement item in source)
                return item.Value;
            return null;
        }
        public static void set_Value(IEnumerable<XElement> source, string value)
        {
            foreach (XElement item in source)
            {
                item.Value = value;
                break;
            }
        }
        public static string get_AttributeValue(IEnumerable<XElement> source, XName name)
        {
            foreach (XElement item in source)
                return (string)item.Attribute(name);
            return null;
        }
        public static void set_AttributeValue(IEnumerable<XElement> source, XName name, string value)
        {
            foreach (XElement item in source)
            {
                item.SetAttributeValue(name, value);
                break;
            }
        }
        public static string get_AttributeValue(XElement source, XName name)
        {
            return (string)source.Attribute(name);
        }
        public static void set_AttributeValue(XElement source, XName name, string value)
        {
            source.SetAttributeValue(name, value);
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static XAttribute CreateAttribute(XName name, object value)
        {
            if (value is null)
            {
                return null;
            }
            return new XAttribute(name, value);
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static XAttribute CreateNamespaceAttribute(XName name, XNamespace ns)
        {
            var a = new XAttribute(name, ns.NamespaceName);
            a.AddAnnotation(ns);
            return a;
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static object RemoveNamespaceAttributes(string[] inScopePrefixes, XNamespace[] inScopeNs, List<XAttribute> attributes, object obj)
        {
            if (obj is not null)
            {
                XElement elem = obj as XElement;
                if (elem is not null)
                {
                    return RemoveNamespaceAttributes(inScopePrefixes, inScopeNs, attributes, elem);
                }
                else
                {
                    IEnumerable elems = obj as IEnumerable;
                    if (elems is not null)
                    {
                        return RemoveNamespaceAttributes(inScopePrefixes, inScopeNs, attributes, elems);
                    }
                }
            }
            return obj;
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static IEnumerable RemoveNamespaceAttributes(string[] inScopePrefixes, XNamespace[] inScopeNs, List<XAttribute> attributes, IEnumerable obj)
        {
            if (obj is not null)
            {
                IEnumerable<XElement> elems = obj as IEnumerable<XElement>;
                if (elems is not null)
                {
                    return elems.Select(new RemoveNamespaceAttributesClosure(inScopePrefixes, inScopeNs, attributes).ProcessXElement);
                }
                else
                {
                    return obj.Cast<object>().Select(new RemoveNamespaceAttributesClosure(inScopePrefixes, inScopeNs, attributes).ProcessObject);
                }
            }
            return obj;
        }
        [DebuggerNonUserCode()]
        [System.Runtime.CompilerServices.CompilerGenerated()]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        private sealed class RemoveNamespaceAttributesClosure
        {
            private readonly string[] m_inScopePrefixes;
            private readonly XNamespace[] m_inScopeNs;
            private readonly List<XAttribute> m_attributes;
            [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            internal RemoveNamespaceAttributesClosure(string[] inScopePrefixes, XNamespace[] inScopeNs, List<XAttribute> attributes)
            {
                m_inScopePrefixes = inScopePrefixes;
                m_inScopeNs = inScopeNs;
                m_attributes = attributes;
            }
            [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            internal XElement ProcessXElement(XElement elem)
            {
                return RemoveNamespaceAttributes(m_inScopePrefixes, m_inScopeNs, m_attributes, elem);
            }
            [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            internal object ProcessObject(object obj)
            {
                XElement elem = obj as XElement;
                if (elem is not null)
                {
                    return RemoveNamespaceAttributes(m_inScopePrefixes, m_inScopeNs, m_attributes, elem);
                }
                else
                {
                    return obj;
                }
            }
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static XElement RemoveNamespaceAttributes(string[] inScopePrefixes, XNamespace[] inScopeNs, List<XAttribute> attributes, XElement e)
        {
            if (e is not null)
            {
                var a = e.FirstAttribute;

                while (a is not null)
                {
                    var nextA = a.NextAttribute;

                    if (a.IsNamespaceDeclaration)
                    {
                        var ns = a.Annotation<XNamespace>();
                        string prefix = a.Name.LocalName;

                        if (ns is not null)
                        {
                            if (inScopePrefixes is not null && inScopeNs is not null)
                            {
                                int lastIndex = inScopePrefixes.Length - 1;

                                for (int i = 0, loopTo = lastIndex; i <= loopTo; i++)
                                {
                                    string currentInScopePrefix = inScopePrefixes[i];
                                    var currentInScopeNs = inScopeNs[i];
                                    if (prefix.Equals(currentInScopePrefix))
                                    {
                                        if (ns == currentInScopeNs)
                                        {
                                            // prefix and namespace match.  Remove the unneeded ns attribute 
                                            a.Remove();
                                        }

                                        // prefix is in scope but refers to something else.  Leave the ns attribute. 
                                        a = null;
                                        break;
                                    }
                                }
                            }

                            if (a is not null)
                            {
                                // Prefix is not in scope 
                                // Now check whether it's going to be in scope because it is in the attributes list 

                                if (attributes is not null)
                                {
                                    int lastIndex = attributes.Count - 1;
                                    for (int i = 0, loopTo1 = lastIndex; i <= loopTo1; i++)
                                    {
                                        var currentA = attributes[i];
                                        string currentInScopePrefix = currentA.Name.LocalName;
                                        var currentInScopeNs = currentA.Annotation<XNamespace>();
                                        if (currentInScopeNs is not null)
                                        {
                                            if (prefix.Equals(currentInScopePrefix))
                                            {
                                                if (ns == currentInScopeNs)
                                                {
                                                    // prefix and namespace match.  Remove the unneeded ns attribute 
                                                    a.Remove();
                                                }

                                                // prefix is in scope but refers to something else.  Leave the ns attribute. 
                                                a = null;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (a is not null)
                                {
                                    // Prefix is definitely not in scope  
                                    a.Remove();
                                    // namespace is not defined either.  Add this attributes list 
                                    attributes.Add(a);
                                }
                            }
                        }
                    }

                    a = nextA;
                }
            }
            return e;
        }

    }
}