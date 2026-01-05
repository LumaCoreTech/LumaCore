// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;
using System.Reflection;

using LumaCore.Ui.Web.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace LumaCore.Ui.Web.Components;

/// <summary>
/// Validates DataAnnotations with localized error messages using IStringLocalizer.
/// </summary>
/// <remarks>
/// This component replaces the standard DataAnnotationsValidator and automatically
/// localizes validation error messages by resolving <c>ErrorMessage</c> keys through <see cref="IStringLocalizer"/>.
/// </remarks>
public sealed class LocalizedDataAnnotationsValidator : ComponentBase, IDisposable
{
	private ValidationMessageStore? mMessageStore;

	/// <inheritdoc/>
	public void Dispose()
	{
		if (CurrentEditContext != null)
		{
			CurrentEditContext.OnValidationRequested -= OnValidationRequested;
			CurrentEditContext.OnFieldChanged -= OnFieldChanged;
		}
	}

	/// <summary>
	/// Gets or sets the current EditContext cascaded from the parent EditForm.
	/// </summary>
	[CascadingParameter]
	private EditContext? CurrentEditContext { get; set; }

	/// <summary>
	/// Gets or sets the localizer used to provide localized JSON-based strings for the component.
	/// </summary>
	/// <remarks>
	/// The localizer enables retrieval of culture-specific resources, allowing the component to display text in the
	/// user's preferred language. This property is typically injected by the framework and should not be set manually.
	/// </remarks>
	[Inject]
	private JsonStringLocalizer Localizer { get; set; } = null!;

	/// <inheritdoc/>
	protected override void OnInitialized()
	{
		// Ensure we have an EditContext to work with.
		if (CurrentEditContext == null)
		{
			throw new InvalidOperationException(
				$"{nameof(LocalizedDataAnnotationsValidator)} requires a cascading parameter " +
				$"of type {nameof(EditContext)}. For example, you can use {nameof(LocalizedDataAnnotationsValidator)} " +
				$"inside an {nameof(EditForm)}.");
		}

		// Create message store for validation messages.
		mMessageStore = new ValidationMessageStore(CurrentEditContext);

		// Subscribe to validation events.
		CurrentEditContext.OnValidationRequested += OnValidationRequested;
		CurrentEditContext.OnFieldChanged += OnFieldChanged;
	}

	/// <summary>
	/// Localizes a validation error message using <see cref="IStringLocalizer"/>.
	/// </summary>
	/// <param name="errorMessage">The error message key to localize.</param>
	/// <returns>
	/// The localized error message if a translation is found, otherwise the original error message.
	/// Returns an empty string if <paramref name="errorMessage"/> is <see langword="null"/> or empty.
	/// </returns>
	/// <remarks>
	/// If the <see cref="IStringLocalizer"/> cannot find a resource for the given key, the original
	/// error message is returned unchanged. This ensures validation messages are always displayed,
	/// even if translations are missing.
	/// </remarks>
	private string LocalizeMessage(string? errorMessage)
	{
		// Return empty string if no message is provided.
		if (string.IsNullOrEmpty(errorMessage))
			return string.Empty;

		// Look up the localized string using the error message as the key.
		// This assumes that the ErrorMessage property contains a resource key.
		LocalizedString localizedString = Localizer[errorMessage];

		// If resource was found, use it; otherwise use original message.
		return localizedString.ResourceNotFound ? errorMessage : localizedString.Value;
	}

	/// <summary>
	/// Handles field changed events by clearing messages for the changed field and re-validating it.
	/// </summary>
	/// <param name="sender">The event sender.</param>
	/// <param name="e">The field changed event arguments containing the field identifier.</param>
	private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
	{
		// Clear messages for this field only.
		mMessageStore?.Clear(e.FieldIdentifier);

		// Re-validate this specific field.
		ValidateField(e.FieldIdentifier);
	}

	/// <summary>
	/// Handles validation requests by clearing existing messages and validating the entire model.
	/// </summary>
	/// <param name="sender">The event sender.</param>
	/// <param name="e">The validation requested event arguments.</param>
	private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
	{
		mMessageStore?.Clear();
		ValidateModel();
	}

	/// <summary>
	/// Validates a specific field and adds localized validation messages to the message store.
	/// </summary>
	/// <param name="fieldIdentifier">The field identifier specifying which field to validate.</param>
	/// <remarks>
	/// Only validates the specified field, not the entire model.
	/// Localized error messages are added to the validation message store.
	/// </remarks>
	private void ValidateField(FieldIdentifier fieldIdentifier)
	{
		PropertyInfo? propertyInfo = fieldIdentifier.Model.GetType()
			.GetProperty(
				fieldIdentifier.FieldName,
				BindingFlags.Public | BindingFlags.Instance);

		// If the property doesn't exist, exit early.
		// This should not normally happen.
		if (propertyInfo == null)
			return;

		// Get the current value of the property to validate.
		object? propertyValue = propertyInfo.GetValue(fieldIdentifier.Model);

		// Create validation context for the specific property.
		// This tells the validator which property to validate.
		var validationContext = new ValidationContext(fieldIdentifier.Model)
		{
			MemberName = fieldIdentifier.FieldName
		};

		// Validate the specific property only.
		var validationResults = new List<ValidationResult>();
		Validator.TryValidateProperty(
			propertyValue,
			validationContext,
			validationResults);

		// Add localized messages for this field.
		foreach (ValidationResult validationResult in validationResults)
		{
			string localizedMessage = LocalizeMessage(validationResult.ErrorMessage);
			mMessageStore?.Add(fieldIdentifier, localizedMessage);
		}

		CurrentEditContext?.NotifyValidationStateChanged();
	}

	/// <summary>
	/// Validates the entire model and adds localized validation messages to the message store.
	/// </summary>
	/// <remarks>
	/// Uses DataAnnotations validation attributes on the model to determine validity.
	/// All validation error messages are localized using <see cref="IStringLocalizer"/>.
	/// </remarks>
	private void ValidateModel()
	{
		// If there's no model, exit early.
		if (CurrentEditContext?.Model == null)
			return;

		// Create validation context for the entire model.
		var validationContext = new ValidationContext(CurrentEditContext.Model);
		var validationResults = new List<ValidationResult>();

		// Validate entire model.
		Validator.TryValidateObject(
			CurrentEditContext.Model,
			validationContext,
			validationResults,
			validateAllProperties: true);

		// Add localized messages.
		foreach (ValidationResult validationResult in validationResults)
		{
			if (validationResult.MemberNames.Any())
			{
				// Property-level validation error.
				// Add message for each member.
				foreach (string memberName in validationResult.MemberNames)
				{
					var fieldIdentifier = new FieldIdentifier(CurrentEditContext.Model, memberName);
					string localizedMessage = LocalizeMessage(validationResult.ErrorMessage);
					mMessageStore?.Add(fieldIdentifier, localizedMessage);
				}
			}
			else
			{
				// Model-level validation error
				// Add message without specific field.
				var fieldIdentifier = new FieldIdentifier(CurrentEditContext.Model, string.Empty);
				string localizedMessage = LocalizeMessage(validationResult.ErrorMessage);
				mMessageStore?.Add(fieldIdentifier, localizedMessage);
			}
		}

		// Notify that the validation state has changed.
		CurrentEditContext.NotifyValidationStateChanged();
	}
}
