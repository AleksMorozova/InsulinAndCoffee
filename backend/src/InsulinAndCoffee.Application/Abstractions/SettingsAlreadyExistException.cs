namespace InsulinAndCoffee.Application.Abstractions;

// Infrastructure reports only a violation of the settings UserId uniqueness constraint.
public sealed class SettingsAlreadyExistException(Exception innerException)
    : Exception("Settings already exist for this user.", innerException);
