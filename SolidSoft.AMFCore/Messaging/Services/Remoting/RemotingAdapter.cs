﻿using System;
using System.Collections;
using System.Reflection;
using SolidSoft.AMFCore.Invocation;
using SolidSoft.AMFCore.Messaging.Services;
using SolidSoft.AMFCore.Messaging.Messages;
using SolidSoft.AMFCore.Messaging;

namespace SolidSoft.AMFCore.Remoting
{
	/// <summary>
	/// This type supports the AMFCore infrastructure and is not intended to be used directly from your code.
	/// </summary>
	public class RemotingAdapter : ServiceAdapter
	{
		public RemotingAdapter()
		{
		}

		public override object Invoke(IMessage message)
		{
			object result = null;
			RemotingMessage remotingMessage = message as RemotingMessage;
			string operation = remotingMessage.operation;
            string className = this.DestinationSettings.Properties["source"] as string;
            //This property is provided for backwards compatibility. The best practice, however, is to not expose the underlying source of a 
            //RemoteObject destination on the client and only one source to a destination.
            if (remotingMessage.source != null && remotingMessage.source != string.Empty)
            {
                if (className == "*")
                    className = remotingMessage.source;
                if (className != remotingMessage.source)
                {
                    string msg = __Res.GetString(__Res.Type_MismatchMissingSource, remotingMessage.source, this.DestinationSettings.Properties["source"] as string);
                    throw new MessageException(msg, new TypeLoadException(msg));
                }
            }

            if( className == null )
				throw new TypeInitializationException("null", null);

            //Cache check
            string source = className + "." + operation;
			IList parameterList = remotingMessage.body as IList;

			FactoryInstance factoryInstance = this.Destination.GetFactoryInstance();
			factoryInstance.Source = className;
			object instance = factoryInstance.Lookup();

			if( instance != null )
			{
				Type type = instance.GetType();
                bool isAccessible = TypeHelper.GetTypeIsAccessible(type);
                if (!isAccessible)
                {
                    string msg = __Res.GetString(__Res.Type_InitError, type.FullName);
                    throw new MessageException(msg, new TypeLoadException(msg));
                }

				try
				{
					MethodInfo mi = MethodHandler.GetMethod(type, operation, parameterList);
					if( mi != null )
					{
						ParameterInfo[] parameterInfos = mi.GetParameters();
						object[] args = new object[parameterInfos.Length];
						parameterList.CopyTo(args, 0);
						TypeHelper.NarrowValues( args, parameterInfos);
						InvocationHandler invocationHandler = new InvocationHandler(mi);
						result = invocationHandler.Invoke(instance, args);
					}
					else
						throw new MessageException(new MissingMethodException(className, operation));
				}
				catch(TargetInvocationException exception)
				{
					MessageException messageException = null;
                    if (exception.InnerException is MessageException)
                        messageException = exception.InnerException as MessageException;//User code throws MessageException
					else
                        messageException = new MessageException(exception.InnerException);
					throw messageException;
				}
                catch (Exception exception)
				{
                    MessageException messageException = new MessageException(exception);
					throw messageException;
				}
			}
			else
				throw new MessageException( new TypeInitializationException(className, null) );

            return result;
		}
	}
}