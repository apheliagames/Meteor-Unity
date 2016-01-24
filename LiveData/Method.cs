using System;
using System.Collections;
using UnityEngine;
using Meteor.Extensions;

namespace Meteor
{
	public class Method : IMethod
	{
		public Method ()
		{
			Updated = false;
		}

		public MethodMessage Message;

		public event MethodHandler OnUntypedResponse;

		public string Name {
			get;
			protected set;
		}

		public static Method Call (string name, params object[] args)
		{
			var methodCall = LiveData.Instance.Call (name, args);
			methodCall.Name = name;
			return methodCall;
		}

		public virtual void Callback (Error error, object response)
		{
			if (OnUntypedResponse != null) {
				OnUntypedResponse (error, response);
			}
		}

		public virtual Type ResponseType {
			get {
				return typeof(IDictionary);
			}
		}

		#region IMethod implementation

		public object UntypedResponse {
			get;
			protected set;
		}

		public Error Error {
			get;
			protected set;
		}

		public bool Updated {
			get;
			set;
		}

		public bool Optimized {
			get;
			set;
		}

		#endregion

		protected bool complete;

		protected void completed (Error error, object response)
		{
			UntypedResponse = response;
			Error = error;
			complete = true;
		}

		protected bool Disconnected {
			get {
				return !Connection.Connected && !LiveData.Instance.TimedOut;
			}
		}

		protected virtual void Send ()
		{
			if (Optimized) {
				LiveData.Instance.SendOptimized (Message);
			} else {
				LiveData.Instance.Send (Message);
			}
		}

		protected virtual IEnumerator Execute ()
		{
			// Send the method message over the wire.
			while (Disconnected) {
				yield return null;
			}

			if (LiveData.Instance.TimedOut) {
				Callback (new Error () { error = -1, details = "Connection timed out." }, null);
				yield break;
			}

			// Send the method message over the wire.
			Send();

			// Wait until we get a response.
			while (!(complete && Updated)) {
				yield return null;
			}

			// Clear the completed handler.
			OnUntypedResponse -= completed;

			yield break;
		}

		public virtual Coroutine ExecuteAsync (MethodHandler callback = null)
		{
			this.OnUntypedResponse += callback;
			return (Coroutine)this;
		}

		public static implicit operator Coroutine (Method method)
		{
			if (method == null) {
				return null;
			}
			method.OnUntypedResponse += method.completed;
			return CoroutineHost.Instance.StartCoroutine (method.Execute ());
		}

		protected sealed class MethodHost : MonoSingleton<MethodHost>
		{

		}
	}

	public class Method<TResponseType> : Method
	{
		public Method ()
		{
			Updated = false;
		}

		public static new Method<TResponseType> Call (string name, params object[] args)
		{
			var methodCall = LiveData.Instance.Call<TResponseType> (name, args);
			methodCall.Name = name;
			return methodCall;
		}

		public event MethodHandler<TResponseType> OnResponse;

		public TResponseType Response {
			get {
				return UntypedResponse == null ? default(TResponseType) : (TResponseType)UntypedResponse;
			}
			private set {
				UntypedResponse = value;
			}
		}

		#region IMethod implementation

		public override void Callback (Error error, object response)
		{
			TResponseType r = default(TResponseType);
			try {
				if (response != null) {
					r = response.Coerce<TResponseType> ();
				} else if (response == null
				           && typeof(TResponseType).IsValueType
				           && error == null) {
					Debug.LogError (string.Format ("Returned null when a value type was expected and no error was found.\nMethod: {0}", this));
				}
			} catch (JsonFx.Json.JsonTypeCoercionException ex) {
				if (error == null) {
					Debug.LogWarning (string.Format ("Failed to convert method response type to specified type in call and no error was found.\nMethod: {0}", this));
				}
			}

			if (OnResponse != null) {
				OnResponse (error, r);
			} else {
				base.Callback (error, response);
			}
		}

		public override Type ResponseType {
			get {
				return typeof(TResponseType);
			}
		}

		protected void typedCompleted (Error error, TResponseType response)
		{
			Response = response;
			Error = error;
			complete = true;
		}

		protected override IEnumerator Execute ()
		{
			// Send the method message over the wire.
			while (Disconnected) {
				yield return null;
			}

			if (LiveData.Instance.TimedOut) {
				Callback (new Error () { error = -1, details = "Connection timed out." }, null);
				yield break;
			}

			Send ();

			// Wait until we get a response.
			while (!(complete && Updated)) {
				yield return null;
			}

			// Clear the completed handler.
			OnResponse -= typedCompleted;

			yield break;
		}

		public virtual Coroutine ExecuteAsync (MethodHandler<TResponseType> callback = null)
		{
			this.OnResponse += callback;
			return (Coroutine)this;
		}

		public static implicit operator Coroutine (Method<TResponseType> method)
		{
			if (method == null) {
				return null;
			}
			method.OnResponse += method.typedCompleted;
			return CoroutineHost.Instance.StartCoroutine (method.Execute ());
		}

		public override string ToString ()
		{
			return string.Format ("[Method: Name={0}, Response={1}, ResponseType={2}]", Name, Response, ResponseType.Name);
		}

		#endregion
	}
}

