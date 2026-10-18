using System.Text.Json;
using FluentValidation;
using Mita.Api.Common.Settings;

namespace Mita.Api.Features.Settings.UpdateSettings;

public sealed class UpdateSettingsValidator : AbstractValidator<UpdateSettingsCommand>
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";

    public UpdateSettingsValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.BaseCurrency)
            .Must(value => !SettingsWire.IsMissing(value)).WithErrorCode(Required)
            .Must(value => SettingsWire.TryParseCurrency(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(command => command.Locale)
            .Must(value => !SettingsWire.IsMissing(value)).WithErrorCode(Required)
            .Must(value => SettingsWire.TryParseLocale(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(command => command.MonthStartDay)
            .Must(value => !SettingsWire.IsMissing(value)).WithErrorCode(Required)
            .Must(value => SettingsWire.TryParseMonthStart(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(command => command.Theme)
            .Must(value => !SettingsWire.IsMissing(value)).WithErrorCode(Required)
            .Must(value => SettingsWire.TryParseTheme(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(command => command.Notifications)
            .Must(value => !SettingsWire.IsMissing(value)).WithErrorCode(Required)
            .Must(value => SettingsWire.TryParseNotifications(value, out _)).WithErrorCode(InvalidValue);
    }
}