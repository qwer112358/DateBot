using CSharpFunctionalExtensions;
using DateBot.Domain.Matches;

namespace DateBot.Application.Replies;

/// <param name="Rejection">Why the LLM text was not used, when <see cref="Source"/> is Template.</param>
public sealed record ComposedReply(ReplyText Text, ReplySource Source, Maybe<string> Rejection);
