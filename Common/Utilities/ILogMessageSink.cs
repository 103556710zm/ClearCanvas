﻿namespace ClearCanvas.Common.Utilities
{
	public class LogMessage
	{
		public LogMessage()
		{
			
		}

		public LogMessage(LogLevel level, string message, params object[] arguments)
		{
			Level = level;
			Message = message;
			Arguments = arguments;
		}

		public LogLevel Level { get; set; }
		public string Message { get; set; }
		public object[] Arguments { get; set; }
	}

	/// <summary>
	/// Defines an interface to an object that acts as a log message sink.
	/// </summary>
	public interface ILogMessageSink
	{
		/// <summary>
		/// Writes the specified message to the sink.
		/// </summary>
		/// <param name="message"></param>
		void Write(LogMessage message);
	}
}
