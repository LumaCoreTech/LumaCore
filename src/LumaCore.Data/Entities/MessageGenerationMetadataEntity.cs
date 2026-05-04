// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Data.Entities;

/// <summary>
/// Stores metadata about AI-generated messages for diagnostics and analytics.
/// </summary>
/// <remarks>
///     <para>
///     This entity captures detailed information about how an AI response was generated, including the model used,
///     token consumption, timing, and inference parameters. This data is valuable for cost tracking, performance
///     analysis, and debugging.
///     </para>
///     <para>
///     Only messages sent by personas have associated generation metadata. User messages do not have this entity.
///     The relationship is 1:0..1 (a message may or may not have metadata).
///     </para>
///     <para>
///         <b>Keys:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="MessageId"/> is both the primary key and a foreign key to <see cref="MessageEntity"/>.
///             This models a 1:0..1 relationship (a message may or may not have metadata).
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Sensitive data:</b>
///     <see cref="FullPrompt"/> may contain user content and system prompts.
///     Treat it as potentially sensitive and consider disabling or redacting it in production.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class MessageGenerationMetadataEntity
{
	// --- 1. Primary key (also foreign key to MessageEntity) ---

	/// <summary>
	/// Gets or sets the foreign key to the message. Also serves as the primary key.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is both the PK and FK, creating a 1:0..1 relationship with <see cref="MessageEntity"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Primary key.
	///     </para>
	/// </remarks>
	public MessageId MessageId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the message.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The relationship is required at the database level via <see cref="MessageId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public MessageEntity? Message { get; set; }

	// --- 2. Public identifier (none) ---

	// --- 3. Foreign keys + Navigation properties ---

	/// <summary>
	/// Gets or sets the foreign key to the model endpoint that generated this message.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Points to <see cref="ModelEndpointEntity.Id"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index.
	///     </para>
	/// </remarks>
	public ModelEndpointId ModelEndpointId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the model endpoint that generated this message.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The relationship is required at the database level via <see cref="ModelEndpointId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ModelEndpointEntity? ModelEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the foreign key to the system prompt used for generation.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <see langword="null"/> if no system prompt was used.
	///     </para>
	///     <para>
	///     Configured with <see cref="Microsoft.EntityFrameworkCore.DeleteBehavior.SetNull"/> so that historical
	///     generation metadata can remain even if prompts are removed.
	///     </para>
	/// </remarks>
	public SystemPromptId? SystemPromptId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the system prompt.
	/// </summary>
	/// <remarks>
	///     <para>
	///     May be <see langword="null"/> if no system prompt was used or if it was deleted and the foreign key was set to
	///     <see langword="null"/> (see <see cref="SystemPromptId"/>).
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public SystemPromptEntity? SystemPrompt { get; set; }

	// --- 4. Timestamps (none) ---

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the identifier of the model used to generate the response.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Examples: <c>mistral:7b</c>, <c>llama3.1:8b-instruct-q4_0</c>, <c>gpt-4-turbo</c>
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ModelIdentifierMaxLength"/>.
	///     </para>
	/// </remarks>
	public string Model { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the complete prompt sent to the model including conversation history.
	/// </summary>
	/// <remarks>
	/// Stored for debugging and reproducibility. Can be large for long conversations.
	/// Consider the storage implications before enabling this for all messages.
	/// </remarks>
	// ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
	public string? FullPrompt { get; set; }

	/// <summary>
	/// Gets or sets the number of tokens in the input prompt.
	/// </summary>
	/// <remarks>
	/// Includes the system prompt and conversation history sent to the model.
	/// </remarks>
	public int PromptTokens { get; set; }

	/// <summary>
	/// Gets or sets the number of tokens in the generated response.
	/// </summary>
	/// <remarks>
	/// Tokenization is provider/model specific.
	/// </remarks>
	public int CompletionTokens { get; set; }

	/// <summary>
	/// Gets or sets the time taken to generate the response.
	/// </summary>
	/// <remarks>
	/// Measures the total duration from request submission to stream completion.
	/// </remarks>
	public TimeSpan ResponseTime { get; set; }

	/// <summary>
	/// Gets or sets the maximum tokens parameter used for generation.
	/// </summary>
	/// <remarks>
	/// The upper limit on response length. Actual response may be shorter.
	/// <see langword="null"/> if the provider did not expose or use this concept.
	/// </remarks>
	public int? MaxTokens { get; set; }

	/// <summary>
	/// Gets or sets the temperature parameter used for generation.
	/// </summary>
	/// <remarks>
	/// Controls randomness: lower values (0.0-0.3) are more deterministic, higher values (0.7-1.0) more creative.
	/// <see langword="null"/> if not provided by the underlying model/provider.
	/// </remarks>
	public double? Temperature { get; set; }

	/// <summary>
	/// Gets or sets the top-p (nucleus sampling) parameter.
	/// </summary>
	/// <remarks>
	/// Alternative to temperature for controlling randomness. Typically between 0.0 and 1.0.
	/// <see langword="null"/> if not provided by the underlying model/provider.
	/// </remarks>
	public double? TopP { get; set; }

	// --- 6. Collection navigation properties (none) ---

	/// <summary>
	/// Creates a persistence-safe copy of an existing metadata instance for a specific message.
	/// </summary>
	/// <param name="messageId">The message identifier to associate with the returned metadata.</param>
	/// <param name="source">The source metadata values.</param>
	/// <param name="storeFullPrompt">
	/// <see langword="true"/> to include <see cref="FullPrompt"/>; otherwise <see langword="false"/>.
	/// </param>
	/// <returns>A new <see cref="MessageGenerationMetadataEntity"/> instance suitable for persistence.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="messageId"/> is less than or equal to 0.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Navigation properties (e.g. <see cref="Message"/>, <see cref="SystemPrompt"/>) are intentionally not copied.
	/// This avoids attaching or persisting an unintended EF Core entity graph and keeps this copy suitable for
	/// persistence via the <see cref="MessageId"/> and <see cref="SystemPromptId"/> foreign keys.
	/// </remarks>
	internal static MessageGenerationMetadataEntity CreateForMessage(
		MessageId                       messageId,
		MessageGenerationMetadataEntity source,
		bool                            storeFullPrompt)
	{
		if (messageId.Value <= 0) throw new ArgumentOutOfRangeException(nameof(messageId));
		ArgumentNullException.ThrowIfNull(source);

		return new MessageGenerationMetadataEntity
		{
			MessageId = messageId,
			ModelEndpointId = source.ModelEndpointId,
			Model = source.Model,
			PromptTokens = source.PromptTokens,
			CompletionTokens = source.CompletionTokens,
			ResponseTime = source.ResponseTime,
			MaxTokens = source.MaxTokens,
			Temperature = source.Temperature,
			TopP = source.TopP,
			SystemPromptId = source.SystemPromptId,
			FullPrompt = storeFullPrompt ? source.FullPrompt : null
		};
	}
}
