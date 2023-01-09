using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TinyJson;

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
					int payloadIndex = 0;
					int idIndex = 0;

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
						Func<string?, string, object[]>? parameterCreator = null;

						// Id
						if(idParameter != null)
						{
							if (!CreateIdParser(idParameter.ParameterType, out var idMapper) || idMapper == null)
								throw new ArgumentException($"'{GetType().Name}.{method.Name}()' has an id parameter with unexpected type, expected a string, any integer type, or GUID");
							
							parameterCreator = (_, id) => new[] { idMapper(id) }; 

							if (isAsync)
							{
								// handler.handler = async (string? id, string? payload) => { };
								throw new NotImplementedException();
							}
							else
							{
								handler.handler = (string? id, string? type, string? payload) =>
								{
									method.Invoke(this, parameterCreator(null, id!));
									return Task.CompletedTask;
								};
							}
						}
						// Payload
						else if(payloadParameter != null)
						{
							var payloadParser = CreatePayloadParser(payloadParameter.ParameterType);
							parameterCreator = (type, payload) => new[] { payloadParser(type, payload) }; 

							if (isAsync)
							{
								// handler.handler = async (string? id, string? payload) => { };
								throw new NotImplementedException();
							}
							else
							{
								handler.handler = (string? id, string? type, string? payload) =>
								{
									method.Invoke(this, parameterCreator(type, payload!));
									return Task.CompletedTask;
								};
							}
						}
					}
					// ID & Payload
					else
					{
						Func<string, string?, string, object[]>? parameterCreator = null;
						if (!CreateIdParser(idParameter.ParameterType, out var idParser) || idParser == null)
							throw new ArgumentException($"'{GetType().Name}.{method.Name}()' has an id parameter with unexpected type, expected a string, any integer type, or GUID");
						var payloadParser = CreatePayloadParser(payloadParameter.ParameterType);

						if(payloadIndex == 0) parameterCreator = (id, type, payload) => new[] { payloadParser(type, payload), idParser(id) };
						else                  parameterCreator = (id, type, payload) => new[] { idParser(id), payloadParser(type, payload) };

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

		Func<string?, string, object> CreatePayloadParser(Type type)
		{
			if (type == typeof(string)) return (_, x) => x;
			else if (type.IsValueType)
			{
				     if (type == typeof(long))   return (_, x) => long.Parse(x);
				else if (type == typeof(ulong))  return (_, x) => ulong.Parse(x);
				else if (type == typeof(int))    return (_, x) => int.Parse(x);
				else if (type == typeof(uint))   return (_, x) => uint.Parse(x);
				else if (type == typeof(short))  return (_, x) => short.Parse(x);
				else if (type == typeof(ushort)) return (_, x) => ushort.Parse(x);
				else if (type == typeof(sbyte))  return (_, x) => sbyte.Parse(x);
				else if (type == typeof(byte))   return (_, x) => byte.Parse(x);
				else if (type == typeof(Guid))   return (_, x) => Guid.Parse(x);
				else if (type == typeof(bool))   return (_, x) => bool.Parse(x);
				else throw new NotImplementedException("No body parser for value type " + type.Name);
			}
			else
			{
				return (typeString, payload) =>
				{
					switch (typeString)
					{
						case "csv":  return DeserializeCSV(type, payload);
						case "json": return DeserializeJson(type, payload);
						case "xml":  return DeserializeXML(type, payload);
						default: throw new HandlerException("415 Unsupported Media Type", $"{typeString} is not supported");
					}
				};
			}
		}


		protected virtual object DeserializeCSV(Type type,  string csv)  => throw new HandlerException("415 Unsupported Media Type", "CSV is not supported");

		protected virtual object DeserializeJson(Type type, string json) => JSON.Parse(type, json);
		
		protected virtual object DeserializeXML(Type type,  string xml)  => throw new HandlerException("415 Unsupported Media Type", "XML is not supported");




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
			if (idIndex == 0)                throw new HandlerException("400 Bad Request", "Expected a URL before the ID separator");
			if(idIndex + 1 >= header.Length) throw new HandlerException("400 Bad Request", "Expected an ID after the ID separator");

			var url = idIndex < 0 ? header : header.Substring(0, idIndex);
			var id = idIndex < 0 ? null : header.Substring(idIndex+1);

			var typeIndex = url.IndexOf(' ');
			var type = typeIndex < 0 ? null : url.Substring(0, typeIndex);
			if (typeIndex > -1)
				url = url.Substring(typeIndex+1);

			if (url.Any(x => !char.IsLetterOrDigit(x) && x != '_' && x != '-'))
				throw new HandlerException("400 Bad Request", url, "The URL contains invalid characters");

			if(!endPoints.TryGetValue(url, out var handler))
				throw new HandlerException("404 Not Found", url, $"No end point called '{url}' exists");

			// Handler validation
			if(type != null && handler.payloadIsValueType)
				throw new HandlerException("400 Bad Request", url, $"The type '{type}' is specified but '{url}' only accepts raw values");

			if (id == null && handler.id)  throw new HandlerException("400 Bad Request", url, "Expected id");
			if (id != null && !handler.id) throw new HandlerException("400 Bad Request", url, "Ids not supported");

			// Request without payload
			if (payloadIndex < 0)
			{
				// LogInfo($"Request: {url} {(id != null ? $"[{id}]" : "")}");

				if(handler.payload) throw new HandlerException("400 Bad Request", url, "Expected payload");

				try{ handler.handler!(id, null, null); }
				catch (HandlerException e)
				{
					e.url = url;
					throw e;
				}
			}
			// Request with payload
			else
			{
				// LogInfo($"Request: {url} {(id != null ? $"[{id}]" : "")}\n{payload}");

				if (!handler.payload) throw new HandlerException("400 Bad Request", url, "Payloads not supported");
				if(!handler.payloadIsValueType && type == null) throw new HandlerException("400 Bad Request", url, "A payload type was expected");

				payloadIndex++;
				if (payloadIndex >= message.Length)
					throw new HandlerException("400 Bad Request", url, "Empty payload");
				var payload = message.Substring(payloadIndex);

				try { handler.handler!(id, type, payload); }
				catch (HandlerException e)
				{
					e.url = url;
					throw e;
				}
			}

			return Task.CompletedTask;
		}
	}
}
