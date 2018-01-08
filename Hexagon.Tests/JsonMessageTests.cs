using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Hexagon;

namespace Hexagon.Tests
{
    /// <summary>
    /// Summary description for JsonMessage
    /// </summary>
    [TestClass]
    public class JsonMessageTests
    {
        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        string json = @"{
   'Stores': [
     'Lambton Quay',
     'Willis Street'
   ],
   'Manufacturers': [
     {
       'Name': 'Acme Co',
       'Products': [
        {
          'Name': 'Anvil',
          'Price': 50
        }
      ]
    },
    {
      'Name': 'Contoso',
      'Products': [
        {
          'Name': 'Elbow Grease',
          'Price': 99.95
        },
        {
          'Name': 'Headlight Fluid',
          'Price': 4
        }
      ]
    }
  ]
}";
        [TestMethod]
        public void JsonMustMatch()
        {
            var message = JsonMessage.FromString(json);
            var patterns = new JsonMessagePattern(new string[]
            {
                "$.Manufacturers[?(@.Name == 'Acme Co')]",
                "$.Manufacturers[?(@.Name == 'Contoso')]"
            });
            Assert.IsTrue(patterns.Match(message));
        }

        [TestMethod]
        public void JsonMustNotMatch()
        {
            var message = JsonMessage.FromString(json);
            var patterns = new JsonMessagePattern(new string[]
            {
                "$..Products[?(@.Price >= 100)].Name"
            });
            Assert.IsFalse(patterns.Match(message));
        }
    }
}
