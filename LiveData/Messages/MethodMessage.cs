namespace Meteor
{
	// Make serializable so that the Unity JsonUtility works
	#if UNITY_5_3
	[System.Serializable()]
	#endif
	public class MethodMessage : Message
	{
		const string _method = "method";

		[JsonFx.Json.JsonName("params")]
		[NetJSON.NetJSONProperty("params")]
		public object[] Params;
		public string id;
		public string method;

		public MethodMessage()
		{
			msg = _method;
		}
	}
}

