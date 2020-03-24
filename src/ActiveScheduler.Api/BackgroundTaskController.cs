// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ActiveErrors;
using ActiveRoutes;
using ActiveRoutes.Meta;
using ActiveScheduler.Api.Internal;
using ActiveScheduler.Configuration;
using ActiveScheduler.Hooks;
using ActiveScheduler.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TypeKitchen;
using Error = ActiveErrors.Error;

namespace ActiveScheduler.Api
{
	[Route("tasks")]
	[DynamicController(typeof(BackgroundTaskOptions))]
	[MetaCategory("Operations", "Provides diagnostic tools for server operators at runtime.")]
	[MetaDescription("Manages background tasks.")]
	[DisplayName("Background Tasks")]
	[ApiExplorerSettings(IgnoreApi = false)]
	public class BackgroundTaskController : Controller
	{
		private readonly BackgroundTaskHost _host;
		private readonly IOptionsMonitor<BackgroundTaskOptions> _options;

		private readonly IBackgroundTaskStore _store;
		private readonly ITypeResolver _typeResolver;

		public BackgroundTaskController(IBackgroundTaskStore store, ITypeResolver typeResolver, BackgroundTaskHost host,
			IOptionsMonitor<BackgroundTaskOptions> options)
		{
			_store = store;
			_typeResolver = typeResolver;
			_host = host;
			_options = options;
		}

		[DynamicHttpOptions]
		public IActionResult GetOptions()
		{
			var typesWithPerformMethod = _typeResolver.FindByMethodName(nameof(Handler.PerformAsync));

			var taskTypeNames = typesWithPerformMethod
				.Where(x => !x.IsInterface && !x.IsAbstract)
				.Select(x => x.Name);

			return Ok(new BackgroundTaskOptionsView {Options = _options.CurrentValue, TaskTypes = taskTypeNames});
		}

		[DynamicHttpGet]
		public async Task<IActionResult> GetBackgroundTasks()
		{
			return Ok(await _store.GetAllAsync());
		}

		[DynamicHttpGet]
		[MustHaveQueryParameters("tags")]
		public async Task<IActionResult> GetBackgroundTasksByTag([FromQuery] string tags)
		{
			var tasks = await _store.GetByAnyTagsAsync(tags.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries));

			return Ok(tasks);
		}

		[DynamicHttpGet("{id}")]
		public async Task<IActionResult> GetBackgroundTaskById(string id)
		{
			if (string.IsNullOrWhiteSpace(id) || !int.TryParse(id, out var taskId))
				return new ErrorObjectResult(new Error(ErrorEvents.InvalidRequest, "Invalid task ID"));

			var task = await _store.GetByIdAsync(taskId);
			if (task == null)
				return this.NotFoundError(ErrorEvents.ResourceMissing, "No task found with ID {0}", id);

			return Ok(task);
		}

		[DynamicHttpPost("secondly/{seconds?}")]
		[MetaDescription("Creates a frequently repeating background task, occurring on a schedule every N second(s).")]
		public async Task<IActionResult> CreateSecondlyBackgroundTask([FromBody] CreateBackgroundTaskModel model,
			[FromRoute] int seconds = 0)
		{
			model.Expression = CronTemplates.Secondly(seconds);

			return await CreateBackgroundTask(model);
		}

		[DynamicHttpPost("minutely/{minutes?}/{atSecond?}")]
		[MetaDescription("Creates a frequently repeating background task, occurring on a schedule every N minute(s).")]
		public async Task<IActionResult> CreateMinutelyBackgroundTask([FromBody] CreateBackgroundTaskModel model,
			[FromRoute] int minutes = 0, [FromRoute] int atSecond = 0)
		{
			model.Expression = CronTemplates.Minutely(minutes, atSecond);

			return await CreateBackgroundTask(model);
		}

		[DynamicHttpPost("daily/{atHour?}/{atMinute?}/{atSecond?}")]
		[MetaDescription("Creates a daily repeating background task.")]
		public async Task<IActionResult> CreateDailyBackgroundTask([FromBody] CreateBackgroundTaskModel model,
			[FromRoute] int atHour = 0, [FromRoute] int atMinute = 0, [FromRoute] int atSecond = 0)
		{
			model.Expression = CronTemplates.Daily(atHour, atMinute, atSecond);

			return await CreateBackgroundTask(model);
		}

		[DynamicHttpPost("weekly/{dayOfWeek?}/{atHour?}/{atMinute?}/{atSecond?}")]
		[MetaDescription("Creates a weekly repeating background task.")]
		public async Task<IActionResult> CreateWeeklyBackgroundTask([FromBody] CreateBackgroundTaskModel model,
			[FromRoute] DayOfWeek dayOfWeek = DayOfWeek.Sunday, [FromRoute] int atHour = 0,
			[FromRoute] int atMinute = 0, [FromRoute] int atSecond = 0)
		{
			model.Expression = CronTemplates.Weekly(dayOfWeek, atHour, atMinute, atSecond);

			return await CreateBackgroundTask(model);
		}

		[DynamicHttpPost("monthly/{atDay?}/{atHour?}/{atMinute?}/{atSecond?}")]
		[MetaDescription("Creates a monthly repeating background task.")]
		public async Task<IActionResult> CreateMonthlyBackgroundTask([FromBody] CreateBackgroundTaskModel model,
			[FromRoute] int atDay = 0, [FromRoute] int atHour = 0, [FromRoute] int atMinute = 0,
			[FromRoute] int atSecond = 0)
		{
			model.Expression = CronTemplates.Monthly(atDay, atHour, atMinute, atSecond);

			return await CreateBackgroundTask(model);
		}

		[DynamicHttpPost]
		[MetaDescription("Creates a background task.")]
		public async Task<IActionResult> CreateBackgroundTask([FromBody] CreateBackgroundTaskModel model)
		{
			if (!this.TryValidateModelOrError(model, ErrorEvents.ValidationFailed, ErrorStrings.ValidationFailed,
				out var error))
				return error;

			if (!string.IsNullOrWhiteSpace(model.TaskType))
			{
				var type = _typeResolver.FindByFullName(model.TaskType) ??
				           _typeResolver.FindFirstByName(model.TaskType);
				if (type == null)
					return this.BadRequestError(ErrorEvents.ResourceMissing, "No task type found with name {0}",
						model.TaskType);

				var (success, task) = await _host.TryScheduleTaskAsync(type, model.Data, t =>
				{
					if (model.Tags?.Length > 0)
						t.Tags.AddRange(model.Tags);

					if (!string.IsNullOrWhiteSpace(model.Expression))
						t.Expression = model.Expression;
				});

				if (!success)
				{
					return this.NotAcceptableError(ErrorEvents.CouldNotAcceptWork,
						"Task was not accepted by the server");
				}

				return Accepted(new Uri($"{Request.Path}/{task.Id}", UriKind.Relative));
			}

			return this.NotImplemented();
		}

		[DynamicHttpDelete("{id}")]
		[MetaDescription("Deletes a background task.")]
		public async Task<IActionResult> DeleteBackgroundTask(string id)
		{
			if (string.IsNullOrWhiteSpace(id) || !int.TryParse(id, out var taskId))
				return new ErrorObjectResult(new Error(ErrorEvents.InvalidRequest, "Invalid task ID"));

			var task = await _store.GetByIdAsync(taskId);
			if (task == null)
				return NotFound();

			await _store.DeleteAsync(task);
			return NoContent();
		}
	}
}