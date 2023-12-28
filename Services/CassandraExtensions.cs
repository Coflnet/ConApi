using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Cassandra;

namespace Coflnet.Connections.Services;

public static class CassandraExtensions
{
    public static void RegisterScyllaSession(this IServiceCollection services)
    {
        services.AddSingleton<Cassandra.ISession>(p =>
        {
            var Configuration = p.GetRequiredService<IConfiguration>();
            Console.WriteLine("Connecting to Cassandra...");
            var builder = Cluster.Builder().AddContactPoints(Configuration["CASSANDRA:HOSTS"].Split(","))
                .WithLoadBalancingPolicy(new TokenAwarePolicy(new DCAwareRoundRobinPolicy()))
                .WithCredentials(Configuration["CASSANDRA:USER"], Configuration["CASSANDRA:PASSWORD"])
                .WithDefaultKeyspace(Configuration["CASSANDRA:KEYSPACE"]);

            Console.WriteLine("Connecting to servers " + Configuration["CASSANDRA:HOSTS"]);
            Console.WriteLine("Using keyspace " + Configuration["CASSANDRA:KEYSPACE"]);
            Console.WriteLine("Using replication class " + Configuration["CASSANDRA:REPLICATION_CLASS"]);
            Console.WriteLine("Using replication factor " + Configuration["CASSANDRA:REPLICATION_FACTOR"]);
            Console.WriteLine("Using user " + Configuration["CASSANDRA:USER"]);
            var certificatePaths = Configuration["CASSANDRA:X509Certificate_PATHS"];
            Console.WriteLine("Using certificate paths " + certificatePaths);
            var validationCertificatePath = Configuration["CASSANDRA:X509Certificate_VALIDATION_PATH"];
            if (!string.IsNullOrEmpty(certificatePaths))
            {
                var password = Configuration["CASSANDRA:X509Certificate_PASSWORD"] ?? throw new InvalidOperationException("CASSANDRA:X509Certificate_PASSWORD must be set if CASSANDRA:X509Certificate_PATHS is set.");
                CustomRootCaCertificateValidator certificateValidator = null;
                if (!string.IsNullOrEmpty(validationCertificatePath))
                    certificateValidator = new CustomRootCaCertificateValidator(new X509Certificate2(validationCertificatePath, password));
                var sslOptions = new SSLOptions(
                    // TLSv1.2 is required as of October 9, 2019.
                    // See: https://www.instaclustr.com/removing-support-for-outdated-encryption-mechanisms/
                    SslProtocols.Tls12,
                    false,
                    // Custom validator avoids need to trust the CA system-wide.
                    (sender, certificate, chain, errors) => certificateValidator?.Validate(certificate, chain, errors) ?? true
                ).SetCertificateCollection(new(certificatePaths.Split(',').Select(p => new X509Certificate2(p, password)).ToArray()));
                builder.WithSSL(sslOptions);
            }
            var cluster = builder.Build();
            var session = cluster.Connect(null);
            var defaultKeyspace = cluster.Configuration.ClientOptions.DefaultKeyspace;
            try
            {
                session.CreateKeyspaceIfNotExists(defaultKeyspace, new Dictionary<string, string>()
                {
                    {"class", Configuration["CASSANDRA:REPLICATION_CLASS"]},
                    {"replication_factor", Configuration["CASSANDRA:REPLICATION_FACTOR"]}
                });
                session.ChangeKeyspace(defaultKeyspace);
                Console.WriteLine("Created cassandra keyspace");
            }
            catch (UnauthorizedException)
            {
                Console.WriteLine("User unauthorized to create keyspace, trying to connect directly");
            }
            finally
            {
                session.ChangeKeyspace(defaultKeyspace);
            }
            return session;
        });
    }
}

public class CustomRootCaCertificateValidator
{
    private readonly X509Certificate2 _trustedRootCertificateAuthority;

    public CustomRootCaCertificateValidator(X509Certificate2 trustedRootCertificateAuthority)
    {
        _trustedRootCertificateAuthority = trustedRootCertificateAuthority;
    }

    public bool Validate(X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
        {
            Console.WriteLine("SSL validation failed due to SslPolicyErrors.RemoteCertificateNotAvailable.");
            return false;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
        {
            Console.WriteLine("SSL validation failed due to SslPolicyErrors.RemoteCertificateNameMismatch.");
            return false;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        {
            // verify if the chain is correct
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status == X509ChainStatusFlags.NoError ||
                    status.Status == X509ChainStatusFlags.UntrustedRoot)
                {
                    //Acceptable Status
                }
                else
                {
                    Console.WriteLine(
                        "Certificate chain validation failed. Found chain status {0} ({1}).", status.Status,
                        status.StatusInformation);
                    return false;
                }
            }

            //Now that we have tested to see if the cert builds properly, we now will check if the thumbprint
            //of the root ca matches our trusted one
            var rootCertThumbprint = chain.ChainElements[chain.ChainElements.Count - 1].Certificate.Thumbprint;
            if (rootCertThumbprint != _trustedRootCertificateAuthority.Thumbprint)
            {
                Console.WriteLine(
                    "Root certificate thumbprint mismatch. Expected {0} but found {1}.",
                    _trustedRootCertificateAuthority.Thumbprint, rootCertThumbprint);
                return false;
            }
        }

        return true;
    }
}