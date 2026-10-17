namespace DateBot.Application.Processing.Plans;

public sealed record ExecutePlan(bool CreateMatch, bool LikeBack, bool Message) : ScenarioPlan;
