namespace InsulinAndCoffee.Domain.Enums;

public enum CarbReferenceSource { Unknown = 0, Manual = 1, AiEstimate = 2, Mixed = 3 }

[Flags]
public enum CarbTransformation { None = 0, Calculated = 1, HistoricalReuse = 2 }
