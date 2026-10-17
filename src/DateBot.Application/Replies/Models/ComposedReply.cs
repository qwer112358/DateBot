using CSharpFunctionalExtensions;
using DateBot.Application.Replies.Enums;
using DateBot.Domain.Matches.ValueObjects;

namespace DateBot.Application.Replies.Models;

/// <param name="Rejection">Why the LLM text was not used, when <see cref="Source"/> is Template.</param>
/// <param name="NeedsReview">The model's text was rejected and llm.json says to show the template to you first.</param>
public sealed record ComposedReply(ReplyText Text, ReplySource Source, Maybe<string> Rejection, bool NeedsReview = false);
