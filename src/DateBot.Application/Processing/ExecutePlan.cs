namespace DateBot.Application.Processing;

public sealed record ExecutePlan(bool CreateMatch, bool LikeBack, bool Message) : ScenarioPlan;
