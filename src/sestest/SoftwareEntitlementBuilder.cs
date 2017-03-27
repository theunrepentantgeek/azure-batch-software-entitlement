﻿using Microsoft.Azure.Batch.SoftwareEntitlement.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.Batch.SoftwareEntitlement
{
    /// <summary>
    /// A factory object that tries to create a <see cref="SoftwareEntitlement"/> instance when given 
    /// the <see cref="GenerateCommandLine"/> specified by the user.
    /// </summary>
    public class SoftwareEntitlementBuilder
    {
        // Reference to the generate command line we wrap
        private readonly GenerateCommandLine _commandLine;

        // Reference to a store in which we can search for certificates
        private readonly CertificateStore _certificateStore = new CertificateStore();

        // Reference to a parser to use for timestamps
        private readonly TimestampParser _timestampParser = new TimestampParser();

        // A steady reference for "now"
        private readonly DateTimeOffset _now = DateTimeOffset.Now;

        /// <summary>
        /// Build an instance of <see cref="SoftwareEntitlement"/> from the information supplied on the 
        /// command line by the user
        /// </summary>
        /// <param name="commandLine">Command line parameters supplied by the user.</param>
        /// <returns>Either a usable (and completely valid) <see cref="SoftwareEntitlement"/> or a set 
        /// of errors.</returns>
        public static Errorable<SoftwareEntitlement> Build(GenerateCommandLine commandLine)
        {
            var builder = new SoftwareEntitlementBuilder(commandLine);
            return builder.Build();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateCommandLine"/> class
        /// </summary>
        /// <param name="commandLine">Options provided on the command line.</param>
        private SoftwareEntitlementBuilder(GenerateCommandLine commandLine)
        {
            _commandLine = commandLine ?? throw new ArgumentNullException(nameof(commandLine));
        }

        /// <summary>
        /// Build an instance of <see cref="SoftwareEntitlement"/> from the information supplied on the 
        /// command line by the user
        /// </summary>
        /// <returns>Either a usable (and completely valid) <see cref="SoftwareEntitlement"/> or a set 
        /// of errors.</returns>
        private Errorable<SoftwareEntitlement> Build()
        {
            var entitlement = new SoftwareEntitlement();
            var errors = new List<string>();

            // readConfiguration - function to read the configuration value
            // applyConfiguration - function to modify our configuration with the value read
            void Configure<V>(Func<Errorable<V>> readConfiguration, Func<V, SoftwareEntitlement> applyConfiguration)
            {
                readConfiguration().Match(
                    whenSuccessful: value => entitlement = applyConfiguration(value),
                    whenFailure: e => errors.AddRange(e));
            }

            Configure(VirtualMachineId, url => entitlement.WithVirtualMachineId(url));
            Configure(NotBefore, notBefore => entitlement.FromInstant(notBefore));
            Configure(NotAfter, notAfter => entitlement.UntilInstant(notAfter));

            if (errors.Any())
            {
                return Errorable.Failure<SoftwareEntitlement>(errors);
            }

            return Errorable.Success(entitlement);
        }

        private Errorable<string> VirtualMachineId()
        {
            if (string.IsNullOrEmpty(_commandLine.VirtualMachineId))
            {
                return Errorable.Failure<string>("No virtual machine identifier specified.");
            }

            return Errorable.Success(_commandLine.VirtualMachineId);
        }

        private Errorable<DateTimeOffset> NotBefore()
        {
            if (string.IsNullOrEmpty(_commandLine.NotBefore))
            {
                // If the user does not specify a start instant for the token, we default to 'now'
                return Errorable.Success(DateTimeOffset.Now);
            }

            return _timestampParser.TryParse(_commandLine.NotBefore, "NotBefore");
        }

        private Errorable<DateTimeOffset> NotAfter()
        {
            if (string.IsNullOrEmpty(_commandLine.NotAfter))
            {
                // If the user does not specify an expiry for the token, we default to 7days from 'now'
                return Errorable.Success(_now + TimeSpan.FromDays(7));
            }

            return _timestampParser.TryParse(_commandLine.NotAfter, "NotAfter");
        }
    }
}