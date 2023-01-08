using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketsNET.Protocols.SEP
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class Route : Attribute
	{
		public readonly string url;
		public Route(string url) => this.url = url;
	}

	public class SimpleEndPointHandler : Handler
	{
		readonly Dictionary<long, WebSocketConnection> connections = new Dictionary<long, WebSocketConnection>();
		readonly Dictionary<WebSocketConnection, long> ids = new Dictionary<WebSocketConnection, long>();
		readonly Queue<long> idsToReuse = new Queue<long>();

		readonly object locker = new object();
		long nextId = 0;

		readonly Dictionary<string, EndPoint> endPoints = new Dictionary<string, EndPoint>();

		
		public SimpleEndPointHandler()
		{
			var methods = GetType()
							.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
							.Where(x => x.GetCustomAttribute<Route>() != null)
							.ToArray()
							;

			foreach(var method in methods)
			{
				// Method
				var isAsync = false;
				if(method.ReturnType != typeof(void))
				{
					if(!method.ReturnType.IsGenericType || method.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
					{

					}
					throw new NotImplementedException("Simple end point async");
				}

				// URL
				var endPointData = method.GetCustomAttribute<Route>()!;
				if(string.IsNullOrWhiteSpace(endPointData.url))
					throw new ArgumentException($"'{GetType().Name}.{method.Name}()' has an empty URL");

				if (endPointData.url.Any(x => !char.IsLetterOrDigit(x) && x != '-' && x != '_'))
					throw new ArgumentException($"'{GetType().Name}.{method.Name}()' has an invalid URL: '{endPointData.url}'");

				if (endPoints.ContainsKey(endPointData.url))
					throw new ArgumentException($"'{GetType().Name}' contains more than one '{endPointData.url}' end points");

				// Parameters
				var parameters = method.GetParameters();
				if (parameters.Length > 2)
					throw new ArgumentException($"'{GetType().Name}.{method.Name}()' has too many parameters, two are the highest allowed (for payload & id)");

				var handler = new EndPoint();
				endPoints[endPointData.url] = handler;

				// Expects no data at all
				if (parameters.Length == 0)
				{
					if (isAsync)
					{
						// handler.handler = async (string? id, string? payload) => { };
						throw new NotImplementedException();
					}
					else
					{
						handler.handler = (string? id, string? type, string? payload) =>
						{
							method.Invoke(this, null);
							return Task.CompletedTask;
						};
					}
				}
				// Expects id or payload, or both
				else
				{
					ParameterInfo? payloadParameter = null;
					ParameterInfo? idParameter = null;
					int payloadIndex, idIndex;

					for(int i = 0; i < parameters.Length; i++)
					{
						switch (parameters[i].Name)
						{
							case "payload":
								handler.payload = true;
								handler.payloadIsValueType = parameters[i].ParameterType == typeof(string) || parameters[i].ParameterType.IsValueType;
								payloadParameter = parameters[i];
								payloadIndex = i;
								break;

							case "id":
								handler.id = true;
								idParameter = parameters[i];
								idIndex = i;
								break;

							default:
								throw new ArgumentException($"'{GetType().Name}.{method.Name}()' has a parameter called '{parameters[i].Name}'. Expected 'payload' or 'id'");
						}
					}

					if (parameters.Length == 1)
					{
						Func<string, object[]>? parameterCreator = null;

						// Id
						if(idParameter != null)
						{
							if (!CreateIdParser(idParameter.ParameterType, out var idMapper) || idMapper == null)
								throw new ArgumentException($"'{GetType().Name}.{method.Name}()' has an id parameter with unexpected type, expected a string, any integer type, or GUID");
							
							parameterCreator = x => new[] { idMapper(x) }; 

							if (isAsync)
							{
								// handler.handler = async (string? id, string? payload) => { };
								throw new NotImplementedException();
							}
							else
							{
								handler.handler = (string? id, string? type, string? payload) =>
								{
									method.Invoke(this, parameterCreator(id));
									return Task.CompletedTask;
								};
							}
						}
						// Payload
						else if(payloadParameter != null)
						{
							var payloadParser = CreatePayloadParser(payloadParameter.ParameterType);
							parameterCreator = x => new[] { payloadParser(x) }; 

							if (isAsync)
							{
								// handler.handler = async (string? id, string? payload) => { };
								throw new NotImplementedException();
							}
							else
							{
								handler.handler = (string? id, string? type, string? payload) =>
								{
									method.Invoke(this, new[] { payload });
									return Task.CompletedTask;
								};
							}
						}
					}
					else
					{
						throw new NotImplementedException("Simple end point paramaters");
					}
				}
			}
		}

		bool CreateIdParser(Type type, out Func<string, object>? func)
		{
			if (type == typeof(string))      func = x => x;
			else if (type == typeof(long))   func = x => long.Parse(x);
			else if (type == typeof(ulong))  func = x => ulong.Parse(x);
			else if (type == typeof(int))    func = x => int.Parse(x);
			else if (type == typeof(uint))   func = x => uint.Parse(x);
			else if (type == typeof(short))  func = x => short.Parse(x);
			else if (type == typeof(ushort)) func = x => ushort.Parse(x);
			else if (type == typeof(sbyte))  func = x => sbyte.Parse(x);
			else if (type == typeof(byte))   func = x => byte.Parse(x);
			else if (type == typeof(Guid))   func = x => Guid.Parse(x);
			else func = null;
			
			return func != null;
		}

		Func<string, object> CreatePayloadParser(Type type)
		{
			if (type == typeof(string)) return x => x;
			else if (type.IsValueType)
			{
				     if (type == typeof(long))   return x => long.Parse(x);
				else if (type == typeof(ulong))  return x => ulong.Parse(x);
				else if (type == typeof(int))    return x => int.Parse(x);
				else if (type == typeof(uint))   return x => uint.Parse(x);
				else if (type == typeof(short))  return x => short.Parse(x);
				else if (type == typeof(ushort)) return x => ushort.Parse(x);
				else if (type == typeof(sbyte))  return x => sbyte.Parse(x);
				else if (type == typeof(byte))   return x => byte.Parse(x);
				else if (type == typeof(Guid))   return x => Guid.Parse(x);
				else if (type == typeof(bool))   return x => bool.Parse(x);
				else throw new NotImplementedException("No body parser for value type " + type.Name);
			}
			else
			{
				throw new NotImplementedException("No body parser for classes exist yet");
			}
		}

		public sealed override void OnConnect(WebSocketConnection connection)
		{
			lock (locker)
			{
				var id = nextId++;
				connections[id] = connection;
				ids[connection] = id;
			}
		}

		public sealed override void OnDisconnect(WebSocketConnection connection)
		{
			lock (locker)
			{
				if(ids.TryGetValue(connection, out var id))
				{
					ids.Remove(connection);
					connections.Remove(id);
					idsToReuse.Enqueue(id);
				}
			}
		}

		public sealed override Task HandleAsync(WebSocketConnection connection, string message)
		{
			var payloadIndex = message.IndexOf('\n');
			var header = payloadIndex < 0 ? message : message.Substring(0, payloadIndex);

			var idIndex = header.IndexOf(':');
			if (idIndex == 0)                throw new HandlerException($"400 Bad Request\nExpected a URL before the ID separator");
			if(idIndex + 1 >= header.Length) throw new HandlerException($"400 Bad Request\nExpected an ID after the ID separator");

			var url = idIndex < 0 ? header : header.Substring(0, idIndex);
			var id = idIndex < 0 ? null : header.Substring(idIndex+1);

			var typeIndex = url.IndexOf(' ');
			var type = typeIndex < 0 ? null : url.Substring(0, typeIndex);
			if (typeIndex > -1)
				url = url.Substring(typeIndex+1);

			if (url.Any(x => !char.IsLetterOrDigit(x) && x != '_' && x != '-'))
				throw new HandlerException($"400 Bad Request: {url}\nThe URL contains invalid characters");

			if(!endPoints.TryGetValue(url, out var handler))
				throw new HandlerException($"404 Not Found: {url}");

			// Handler validation
			if(type != null && handler.payloadIsValueType)
				throw new HandlerException($"400 Bad Request: {url}\nThe type '{type}' is specified but '{url}' only accepts raw values");

			if (id == null && handler.id)  throw new HandlerException($"400 Bad Request: {url}\nExpected id");
			if (id != null && !handler.id) throw new HandlerException($"400 Bad Request: {url}\nIds not supported");

			// Request without payload
			if (payloadIndex < 0)
			{
				// LogInfo($"Request: {url} {(id != null ? $"[{id}]" : "")}");

				if(handler.payload) throw new HandlerException($"400 Bad Request: {url}\nExpected payload");
				handler.handler!(id, null, null);
			}
			// Request with payload
			else
			{
				// LogInfo($"Request: {url} {(id != null ? $"[{id}]" : "")}\n{payload}");

				if (!handler.payload) throw new HandlerException($"400 Bad Request: {url}\nPayloads not supported");

				payloadIndex++;
				if (payloadIndex >= message.Length)
					throw new HandlerException($"400 Bad Request: {url}\nEmpty payload");
				var payload = message.Substring(payloadIndex);
				handler.handler!(id, type, payload);
			}

			return Task.CompletedTask;
		}
	}
}
