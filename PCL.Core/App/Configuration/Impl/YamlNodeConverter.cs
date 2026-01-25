using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace PCL.Core.App.Configuration.Impl;

// 修改自: https://dotnetfiddle.net/jaG1i1
// 来源: https://stackoverflow.com/a/40727087
// 原作者: Antoine Aubry (是 YamlDotNet 的作者, 真不懂都造出来了为什么不直接把它写到库里)
public static class YamlNodeConverter
{
	public class EventStreamParserAdapter(IEnumerable<ParsingEvent> events) : IParser
	{
		private readonly IEnumerator<ParsingEvent> _enumerator = events.GetEnumerator();

		public ParsingEvent Current => _enumerator.Current;

		public bool MoveNext() => _enumerator.MoveNext();
	}

	public static IParser GetParser(this IEnumerable<ParsingEvent> eventStream)
	{
		return new EventStreamParserAdapter(eventStream);
	}

	public static IEnumerable<ParsingEvent> ConvertToEventStream(this YamlStream stream)
	{
		yield return new StreamStart();
		foreach (var document in stream.Documents)
		{
			foreach (var evt in ConvertToEventStream(document))
			{
				yield return evt;
			}
		}
		yield return new StreamEnd();
	}

	public static IEnumerable<ParsingEvent> ConvertToEventStream(this YamlDocument document)
	{
		yield return new DocumentStart();
		foreach (var evt in ConvertToEventStream(document.RootNode))
		{
			yield return evt;
		}
		yield return new DocumentEnd(false);
	}

	public static IEnumerable<ParsingEvent> ConvertToEventStream(this YamlNode node)
	{
		if (node is YamlScalarNode scalar)
		{
			return _ConvertToEventStream(scalar);
		}

		if (node is YamlSequenceNode sequence)
		{
			return _ConvertToEventStream(sequence);
		}

		if (node is YamlMappingNode mapping)
		{
			return _ConvertToEventStream(mapping);
		}
		
		throw new NotSupportedException($"Unsupported node type: {node.GetType().Name}");
	}

	private static IEnumerable<ParsingEvent> _ConvertToEventStream(YamlScalarNode scalar)
	{
		yield return new Scalar(scalar.Anchor, scalar.Tag, scalar.Value!, scalar.Style, false, false);
	}

	private static IEnumerable<ParsingEvent> _ConvertToEventStream(YamlSequenceNode sequence)
	{
		yield return new SequenceStart(sequence.Anchor, sequence.Tag, false, sequence.Style);
		foreach (var node in sequence.Children)
		{
			foreach (var evt in ConvertToEventStream(node))
			{
				yield return evt;
			}
		}
		yield return new SequenceEnd();
	}

	private static IEnumerable<ParsingEvent> _ConvertToEventStream(YamlMappingNode mapping)
	{
		yield return new MappingStart(mapping.Anchor, mapping.Tag, false, mapping.Style);
		foreach (var pair in mapping.Children)
		{
			foreach (var evt in ConvertToEventStream(pair.Key))
			{
				yield return evt;
			}
			foreach (var evt in ConvertToEventStream(pair.Value))
			{
				yield return evt;
			}
		}
		yield return new MappingEnd();
	}
}
