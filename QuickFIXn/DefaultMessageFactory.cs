using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using QuickFix.Fields;

namespace QuickFix
{
    /// <summary>
    /// The default factory for creating FIX message instances.  (In the v2.0 release, this class should be made sealed.)
    /// </summary>
    public class DefaultMessageFactory : IMessageFactory
    {
        private readonly Dictionary<string, IMessageFactory> _factories = new Dictionary<string, IMessageFactory>();

        public DefaultMessageFactory()
        {
			  var factoryInterface = typeof(IMessageFactory);
			  Regex namingConvention = new Regex("QuickFix.(?<version>[^\\.]+).MessageFactory");
			  // Get all concrete types implementing the IMessageFactory interface
			  //  which are defined in a quickfix assembly
			  //  and which have a public default constructor
			  //  and which are named like QuickFix.FIXVERSION.MessageFactory
			  var messageFactories = from assembly in AppDomain.CurrentDomain.GetAssemblies()
											 where assembly.FullName.StartsWith("QuickFix", StringComparison.InvariantCultureIgnoreCase)
											 from type in assembly.GetExportedTypes() // get public classes
											 where factoryInterface.IsAssignableFrom(type)
												 && type.IsClass
												 && !type.IsAbstract
											 let defaultConstructor = type.GetConstructor(Type.EmptyTypes)
											 let match = namingConvention.Match(type.FullName)
											 where defaultConstructor != null
												 && defaultConstructor.IsPublic
												 && match.Success																						
											 select new {FactoryType = type, FixVersion = match.Groups["version"].Value};

			  foreach(var factory in messageFactories)
			  {
				  var beginStringField = typeof(FixValues.BeginString).GetField(factory.FixVersion, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

				  Debug.Assert(beginStringField != null, "Found messagefactory for fix version that is missing begin string");
				  if(beginStringField != null)
				  {
					  var beginString = beginStringField.GetValue(null).ToString();
					  _factories[beginString] = (IMessageFactory)Activator.CreateInstance( factory.FactoryType);				  
				  }
			  }
        }

        #region IMessageFactory Members

        public Message Create(string beginString, string msgType)
        {
            IMessageFactory f = null;

            // FIXME: This is a hack.  FIXT11 could mean 50 or 50sp1 or 50sp2.
            // We need some way to choose which 50 version it is.
            // Choosing 50 here is not adequate.
            if (beginString.Equals(FixValues.BeginString.FIXT11))
            {
                if (!Message.IsAdminMsgType(msgType))
                    f = _factories[FixValues.BeginString.FIX50];
            }
            if(f != null) // really, this should just be in the previous if-block
                return f.Create(beginString, msgType);



            if (_factories.ContainsKey(beginString) == false)
            {
                Message m = new Message();
                m.Header.SetField(new StringField(QuickFix.Fields.Tags.MsgType, msgType));
                return m;
            }

            f = _factories[beginString];
            return f.Create(beginString, msgType);
        }

        public Group Create(string beginString, string msgType, int groupCounterTag)
        {
            // FIXME: This is a hack.  FIXT11 could mean 50 or 50sp1 or 50sp2.
            // We need some way to choose which 50 version it is.
            // Choosing 50 here is not adequate.
            if(beginString.Equals(FixValues.BeginString.FIXT11))
                return _factories[FixValues.BeginString.FIX50].Create(beginString,msgType,groupCounterTag);

            if (_factories.ContainsKey(beginString))
                return _factories[beginString].Create(beginString, msgType, groupCounterTag);

            throw new UnsupportedVersion(beginString);
        }

        #endregion
    }
}
