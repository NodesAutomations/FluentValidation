#region License
// Copyright (c) .NET Foundation and contributors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// The latest version of this file can be found at https://github.com/FluentValidation/FluentValidation
#endregion

namespace FluentValidation.Validators {
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Internal;
	using Resources;
	using Results;

	public abstract class PropertyValidator : IPropertyValidator {

		/// <inheritdoc />
		public PropertyValidatorOptions Options { get; } = new PropertyValidatorOptions();

		protected PropertyValidator(IStringSource errorMessageSource) {
			if(errorMessageSource == null) errorMessageSource = new StaticStringSource("No default error message has been specified.");
			else if (errorMessageSource is LanguageStringSource l && l.ErrorCodeFunc == null)
				l.ErrorCodeFunc = ctx => Options.ErrorCodeSource?.GetString(ctx);

			Options.ErrorMessageSource = errorMessageSource;
		}

		protected PropertyValidator(string errorMessage) {
			Options.ErrorMessageSource = new StaticStringSource(errorMessage);
		}

		/// <inheritdoc />
		public virtual IEnumerable<ValidationFailure> Validate(PropertyValidatorContext context) {
			if (IsValid(context)) return Enumerable.Empty<ValidationFailure>();

			PrepareMessageFormatterForValidationError(context);
			return new[] { CreateValidationError(context) };

		}

		/// <inheritdoc />
		public virtual async Task<IEnumerable<ValidationFailure>> ValidateAsync(PropertyValidatorContext context, CancellationToken cancellation) {
			if (await IsValidAsync(context, cancellation)) return Enumerable.Empty<ValidationFailure>();

			PrepareMessageFormatterForValidationError(context);
			return new[] {CreateValidationError(context)};
		}

		/// <inheritdoc />
		public virtual bool ShouldValidateAsynchronously(IValidationContext context) {
			// If the user has applied an async condition, then always go through the async path
			// even if validator is being run synchronously.
			if (Options.AsyncCondition != null) return true;
			return false;
		}

		protected abstract bool IsValid(PropertyValidatorContext context);

#pragma warning disable 1998
		protected virtual async Task<bool> IsValidAsync(PropertyValidatorContext context, CancellationToken cancellation) {
			return IsValid(context);
		}
#pragma warning restore 1998

		/// <summary>
		/// Prepares the <see cref="MessageFormatter"/> of <paramref name="context"/> for an upcoming <see cref="ValidationFailure"/>.
		/// </summary>
		/// <param name="context">The validator context</param>
		protected virtual void PrepareMessageFormatterForValidationError(PropertyValidatorContext context) {
			context.MessageFormatter.AppendPropertyName(context.DisplayName);
			context.MessageFormatter.AppendPropertyValue(context.PropertyValue);

			// If there's a collection index cached in the root context data then add it
			// to the message formatter. This happens when a child validator is executed
			// as part of a call to RuleForEach. Usually parameters are not flowed through to
			// child validators, but we make an exception for collection indices.
			if (context.ParentContext.RootContextData.TryGetValue("__FV_CollectionIndex", out var index)) {
				// If our property validator has explicitly added a placeholder for the collection index
				// don't overwrite it with the cached version.
				if (!context.MessageFormatter.PlaceholderValues.ContainsKey("CollectionIndex")) {
					context.MessageFormatter.AppendArgument("CollectionIndex", index);
				}
			}
		}

		/// <summary>
		/// Creates an error validation result for this validator.
		/// </summary>
		/// <param name="context">The validator context</param>
		/// <returns>Returns an error validation result.</returns>
		protected virtual ValidationFailure CreateValidationError(PropertyValidatorContext context) {
			var messageBuilderContext = new MessageBuilderContext(context, Options.ErrorMessageSource, this);

			var error = context.Rule.MessageBuilder != null
				? context.Rule.MessageBuilder(messageBuilderContext)
				: messageBuilderContext.GetDefaultMessage();

			var failure = new ValidationFailure(context.PropertyName, error, context.PropertyValue);
#pragma warning disable 618
			failure.FormattedMessageArguments = context.MessageFormatter.AdditionalArguments;
#pragma warning restore 618
			failure.FormattedMessagePlaceholderValues = context.MessageFormatter.PlaceholderValues;
			failure.ErrorCode = (Options.ErrorCodeSource != null)
				? Options.ErrorCodeSource.GetString(context)
				: ValidatorOptions.Global.ErrorCodeResolver(this);

			if (Options.CustomStateProvider != null) {
				failure.CustomState = Options.CustomStateProvider(context);
			}

			if (Options.SeverityProvider != null) {
				failure.Severity = Options.SeverityProvider(context);
			}

			return failure;
		}
	}
}
