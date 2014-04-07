﻿#region copyright

// Autofac Quartz integration
// https://github.com/alphacloud/Autofac.Extras.Quartz
// Licensed under MIT license.
// Copyright (c) 2014 Alphacloud.Net

#endregion

namespace Autofac.Extras.Quartz
{
    using System;
    using System.Globalization;
    using global::Quartz;
    using global::Quartz.Spi;
    using JetBrains.Annotations;


    [PublicAPI]
    public class AutofacJobFactory : IJobFactory
    {
        /// <summary>
        ///     Job execution context data map key for lifetime scope manager.
        /// </summary>
        /// <remarks>
        ///     <see cref="ILifetimeScope" /> is stored in <see cref="IJobExecutionContext" /> during job execution.
        /// </remarks>
        public const string LifetimeScopeKeyName = "autofac.lifetime.scope";

        private readonly ILifetimeScope _lifetimeScope;
        private readonly string _scopeName;


        /// <summary>
        ///     Initializes a new instance of the <see cref="AutofacJobFactory" /> class.
        /// </summary>
        /// <param name="lifetimeScope">The lifetime scope.</param>
        /// <param name="scopeName">Name of the scope.</param>
        public AutofacJobFactory(ILifetimeScope lifetimeScope, string scopeName = "quartz.job")
        {
            _lifetimeScope = lifetimeScope;
            _scopeName = scopeName;
        }


        /// <summary>
        ///     Called by the scheduler at the time of the trigger firing, in order to
        ///     produce a <see cref="T:Quartz.IJob" /> instance on which to call Execute.
        /// </summary>
        /// <remarks>
        ///     It should be extremely rare for this method to throw an exception -
        ///     basically only the the case where there is no way at all to instantiate
        ///     and prepare the Job for execution.  When the exception is thrown, the
        ///     Scheduler will move all triggers associated with the Job into the
        ///     <see cref="F:Quartz.TriggerState.Error" /> state, which will require human
        ///     intervention (e.g. an application restart after fixing whatever
        ///     configuration problem led to the issue wih instantiating the Job.
        /// </remarks>
        /// <param name="bundle">
        ///     The TriggerFiredBundle from which the <see cref="T:Quartz.IJobDetail" />
        ///     and other info relating to the trigger firing can be obtained.
        /// </param>
        /// <param name="scheduler">a handle to the scheduler that is about to execute the job</param>
        /// <throws>SchedulerException if there is a problem instantiating the Job. </throws>
        /// <returns>
        ///     the newly instantiated Job
        /// </returns>
        public IJob NewJob([NotNull] TriggerFiredBundle bundle, [NotNull] IScheduler scheduler)
        {
            if (bundle == null) throw new ArgumentNullException("bundle");
            if (scheduler == null) throw new ArgumentNullException("scheduler");
            return new JobWrapper(bundle, _lifetimeScope, _scopeName);
        }


        /// <summary>
        ///     Allows the the job factory to destroy/cleanup the job if needed.
        /// </summary>
        public void ReturnJob(IJob job)
        {}


        /// <summary>
        ///     Job execution wrapper.
        /// </summary>
        /// <remarks>
        ///     Creates nested lifetime scope per job execution and resolves Job from Autofac.
        /// </remarks>
        internal class JobWrapper : IJob
        {
            private readonly TriggerFiredBundle _bundle;
            private readonly ILifetimeScope _lifetimeScope;
            private readonly string _scopeName;


            /// <summary>
            ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
            /// </summary>
            public JobWrapper([NotNull] TriggerFiredBundle bundle, [NotNull] ILifetimeScope lifetimeScope,
                [NotNull] string scopeName)
            {
                if (bundle == null) throw new ArgumentNullException("bundle");
                if (lifetimeScope == null) throw new ArgumentNullException("lifetimeScope");
                if (scopeName == null) throw new ArgumentNullException("scopeName");

                _bundle = bundle;
                _lifetimeScope = lifetimeScope;
                _scopeName = scopeName;
            }


            /// <summary>
            ///     Called by the <see cref="T:Quartz.IScheduler" /> when a <see cref="T:Quartz.ITrigger" />
            ///     fires that is associated with the <see cref="T:Quartz.IJob" />.
            /// </summary>
            /// <remarks>
            ///     The implementation may wish to set a  result object on the
            ///     JobExecutionContext before this method exits.  The result itself
            ///     is meaningless to Quartz, but may be informative to
            ///     <see cref="T:Quartz.IJobListener" />s or
            ///     <see cref="T:Quartz.ITriggerListener" />s that are watching the job's
            ///     execution.
            /// </remarks>
            /// <param name="context">The execution context.</param>
            /// <exception cref="SchedulerConfigException">Job cannot be instantiated.</exception>
            public void Execute(IJobExecutionContext context)
            {
                var scope = _lifetimeScope.BeginLifetimeScope(_scopeName);
                try
                {
                    context.Put(LifetimeScopeKeyName, scope);
                    var job = (IJob) scope.Resolve(_bundle.JobDetail.JobType);
                    job.Execute(context);
                }
                catch (Exception ex)
                {
                    throw new SchedulerConfigException(string.Format(CultureInfo.InvariantCulture,
                        "Failed to instantiate Job '{0}' of type '{1}'",
                        _bundle.JobDetail.Key, _bundle.JobDetail.JobType), ex);
                }
                finally
                {
                    context.Put(LifetimeScopeKeyName, null);
                    scope.Dispose();
                }
            }
        }
    }
}