// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Xunit;

namespace LumaCore.Data.Tests.Entities;

/// <summary>
/// Unit tests for <see cref="MessageGenerationMetadataEntity.CreateForMessage"/>.
/// </summary>
/// <remarks>
/// Construction and POCO-property coverage for <see cref="MessageGenerationMetadataEntity"/> is provided by
/// <see cref="EntitySmokeTests.MessageGenerationMetadataEntity_CanSetAllProperties"/>. This file focuses on
/// the behavior of the <see cref="MessageGenerationMetadataEntity.CreateForMessage"/> factory method.
/// </remarks>
[Trait("Category", "Entities")]
public sealed class MessageGenerationMetadataEntityTests
{
	#region CreateForMessage()

	// --- 1. Valid scenarios ---

	/// <summary>
	/// Verifies that <see cref="MessageGenerationMetadataEntity.CreateForMessage"/> copies all scalar fields and
	/// foreign keys from the source, including <see cref="MessageGenerationMetadataEntity.FullPrompt"/>, when
	/// <paramref name="storeFullPrompt"/> is <see langword="true"/>.
	/// </summary>
	/// <param name="storeFullPrompt">Whether to include the full prompt in the copy.</param>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void CreateForMessage_WhenValid_CopiesScalarFieldsAndForeignKeys(bool storeFullPrompt)
	{
		// Arrange
		var messageId = new MessageId(42);
		var source = new MessageGenerationMetadataEntity
		{
			MessageId = new MessageId(99),
			ModelEndpointId = new ModelEndpointId(7),
			Model = "mistral:7b",
			PromptTokens = 100,
			CompletionTokens = 50,
			ResponseTime = TimeSpan.FromSeconds(1.5),
			MaxTokens = 4096,
			Temperature = 0.7,
			TopP = 0.9,
			SystemPromptId = new SystemPromptId(3),
			FullPrompt = "System: You are helpful.\nUser: Hello"
		};

		// Act
		var result = MessageGenerationMetadataEntity.CreateForMessage(
			messageId,
			source,
			storeFullPrompt);

		// Assert — target MessageId is set to the provided value, not the source's.
		Assert.Equal(messageId, result.MessageId);
		Assert.Equal(new ModelEndpointId(7), result.ModelEndpointId);
		Assert.Equal("mistral:7b", result.Model);
		Assert.Equal(100, result.PromptTokens);
		Assert.Equal(50, result.CompletionTokens);
		Assert.Equal(TimeSpan.FromSeconds(1.5), result.ResponseTime);
		Assert.Equal(4096, result.MaxTokens);
		Assert.Equal(0.7, result.Temperature);
		Assert.Equal(0.9, result.TopP);
		Assert.Equal(new SystemPromptId(3), result.SystemPromptId);

		if (storeFullPrompt)
		{
			Assert.Equal("System: You are helpful.\nUser: Hello", result.FullPrompt);
		}
		else
		{
			Assert.Null(result.FullPrompt);
		}
	}

	/// <summary>
	/// Verifies that <see cref="MessageGenerationMetadataEntity.CreateForMessage"/> does not copy navigation
	/// properties (<see cref="MessageGenerationMetadataEntity.Message"/>,
	/// <see cref="MessageGenerationMetadataEntity.ModelEndpoint"/>,
	/// <see cref="MessageGenerationMetadataEntity.SystemPrompt"/>) from the source to avoid attaching an
	/// unintended EF Core entity graph.
	/// </summary>
	[Fact]
	public void CreateForMessage_WhenSourceHasNavigations_DoesNotCopyNavigationProperties()
	{
		// Arrange
		var source = new MessageGenerationMetadataEntity
		{
			ModelEndpointId = new ModelEndpointId(1),
			Model = "test-model",
			Message = new MessageEntity(),
			ModelEndpoint = new ModelEndpointEntity(),
			SystemPrompt = new SystemPromptEntity()
		};

		// Act
		var result = MessageGenerationMetadataEntity.CreateForMessage(
			new MessageId(1),
			source,
			storeFullPrompt: false);

		// Assert — navigations are not propagated, but scalar fields still are.
		Assert.Null(result.Message);
		Assert.Null(result.ModelEndpoint);
		Assert.Null(result.SystemPrompt);
		Assert.Equal(new ModelEndpointId(1), result.ModelEndpointId);
		Assert.Equal("test-model", result.Model);
	}

	// --- 2. Invalid scenarios ---

	/// <summary>
	/// Verifies that <see cref="MessageGenerationMetadataEntity.CreateForMessage"/> throws
	/// <see cref="ArgumentOutOfRangeException"/> when <c>messageId</c> has a non-positive value.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="idValue">The underlying identifier value to test.</param>
	[Theory]
	[InlineData("Zero", 0)]
	[InlineData("Negative", -1)]
	public void CreateForMessage_WhenMessageIdIsNotPositive_ThrowsArgumentOutOfRangeException(
		string scenario,
		long   idValue)
	{
		_ = scenario; // Used by xUnit test display name only.

		// Arrange
		var source = new MessageGenerationMetadataEntity { ModelEndpointId = new ModelEndpointId(1), Model = "m" };

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => MessageGenerationMetadataEntity.CreateForMessage(
			new MessageId(idValue),
			source,
			storeFullPrompt: false));
		Assert.Equal("messageId", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="MessageGenerationMetadataEntity.CreateForMessage"/> throws
	/// <see cref="ArgumentNullException"/> when <c>source</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void CreateForMessage_WhenSourceIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => MessageGenerationMetadataEntity.CreateForMessage(
			new MessageId(1),
			null!,
			storeFullPrompt: false));
		Assert.Equal("source", ex.ParamName);
	}

	#endregion
}
