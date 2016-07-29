﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Hl7.Fhir.Serialization;
using Newtonsoft.Json;
using System.IO;
using System.Xml.Linq;

namespace Hl7.Fhir.Tests
{
    public class JsonAssert
    {
        public static void AreSame(string filename, JObject expected, JObject actual, List<string> errors)
        {
            areSame(filename, expected.Root, actual.Root, errors);
        }

        public static void AreSame(string filename, string expected, string actual, List<string> errors)
        {
            JObject exp = SerializationUtil.JObjectFromReader(SerializationUtil.JsonReaderFromJsonText(expected));
            JObject act = SerializationUtil.JObjectFromReader(SerializationUtil.JsonReaderFromJsonText(actual));

            AreSame(filename, exp, act, errors);
        }

        private static void areSame(string filename, JToken left, JToken right, List<string> errors)
        {
            if ((left.Type == JTokenType.Integer && right.Type == JTokenType.Float) ||
                (left.Type == JTokenType.Float && right.Type == JTokenType.Integer))
            {
                JValue leftVal = (JValue)left;
                JValue rightVal = (JValue)right;

                if (leftVal.ToString() != rightVal.ToString())
                {
                    errors.Add(String.Format("Error comparing values in: {0}:{1}, {2} - {3}",
                                filename, left.Path, leftVal.ToString(), rightVal.ToString()));
                }
                // Assert.AreEqual(leftVal.ToString(), rightVal.ToString());
                // Bug in json.net, will sometimes convert to integer instead of float
                return;
            }

            if (left.Type != right.Type)
                throw new AssertFailedException("Token type is not the same at " + right.Path);

            if (left.Type == JTokenType.Array)
            {
                var la = (JArray)left;
                var ra = (JArray)right;

                if (la.Count != ra.Count)
                    throw new AssertFailedException("Array size is not the same at " + right.Path);

                for (var i = 0; i < la.Count; i++)
                    areSame(filename, la[i], ra[i], errors);
            }

            else if (left.Type == JTokenType.Object)
            {
                var lo = (JObject)left;
                var ro = (JObject)right;

                foreach (var lMember in lo)
                {
                    JToken rMember;

                    // Hack, some examples have empty arrays or objects, these are illegal and will result in missing members after round-tripiing
                    if (lMember.Value is JArray && ((JArray)lMember.Value).Count == 0) continue;
                    if (lMember.Value is JObject && ((JObject)lMember.Value).Count == 0) continue;

                    if (!ro.TryGetValue(lMember.Key, out rMember) || rMember == null)
                        throw new AssertFailedException(String.Format("Expected member {0} not found in actual at " + left.Path, lMember.Key));

                    areSame(filename, lMember.Value, rMember, errors);
                }

                foreach (var rMember in ro)
                {
                    JToken lMember;

                    if (!lo.TryGetValue(rMember.Key, out lMember))
                        throw new AssertFailedException(String.Format("Actual has unexpected extra member {0} at " + right.Path, rMember.Key));
                }

            }

            else if (left.Type == JTokenType.String)
            {
                var lValue = left.ToString();
                var rValue = right.ToString();

                if (lValue.TrimStart().StartsWith("<div"))
                {
                    // Correct incorrect examples
                    lValue.Trim();

                    if (lValue.StartsWith("<div>"))
                        lValue = "<div xmlns='http://www.w3.org/1999/xhtml'>" + lValue.Substring(5);

                    var leftDoc = FhirParser.XDocumentFromXml(lValue);
                    var rightDoc = FhirParser.XDocumentFromXml(rValue);

                    XmlAssert.AreSame(filename, leftDoc, rightDoc);
                }
                else
                {
                    // Hack for timestamps
                    if (lValue.EndsWith("+00:00")) lValue = lValue.Replace("+00:00", "Z");
                    if (rValue.EndsWith("+00:00")) rValue = rValue.Replace("+00:00", "Z");
                    if (lValue.Contains(".000+")) lValue = lValue.Replace(".000+", "+");
                    if (rValue.Contains(".000+")) rValue = rValue.Replace(".000+", "+");
                    if (lValue.Contains(".000Z")) lValue = lValue.Replace(".000Z", "Z");
                    if (rValue.Contains(".000Z")) rValue = rValue.Replace(".000Z", "Z");

                    if (rValue.EndsWith("Z") && lValue.EndsWith("Z"))
                    {
                        if (lValue != rValue)
                        {
                            System.Diagnostics.Trace.WriteLine(
                                String.Format("Error comparing Timestamp values in: {0}, {1} - {2}",
                                filename, lValue, rValue));
                            errors.Add(String.Format("Error comparing Timestamp values in: {0}:{1}, {2} - {3}",
                                        filename, left.Path, lValue, rValue));
                        }

                    }
                    else
                    {
                        if (lValue != rValue)
                        {
                            System.Diagnostics.Trace.WriteLine(
                                String.Format("Error comparing values in: {0}:{3}, {1} - {2}",
                                filename, lValue, rValue, left.Path));
                            // Assert.AreEqual(lValue, rValue, "Error comparing values in:" + filename);
                            errors.Add(String.Format("Error comparing values in: {0}:{1}, {2} - {3}",
                                        filename, left.Path, lValue, rValue));
                        }
                    }

                }
            }

            else
            {
                if (!JToken.DeepEquals(left, right))
                    throw new AssertFailedException(String.Format("Values not the same at " + left.Path));
            }
        }


    }
}