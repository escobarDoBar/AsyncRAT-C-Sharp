﻿using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.IO.Compression;

namespace AsyncRAT_Sharp.Forms
{
    public partial class FormCertificate : Form
    {
        public FormCertificate()
        {
            InitializeComponent();
        }

        private void FormCertificate_Load(object sender, EventArgs e)
        {
            try
            {
                string backup = Application.StartupPath + "\\BackupCertificate.zip";
                if (File.Exists(backup))
                {
                    MessageBox.Show(this, "Found a zip backup, Extracting (BackupCertificate.zip)", "Certificate backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ZipFile.ExtractToDirectory(backup, Application.StartupPath);
                    Settings.ServerCertificate = new X509Certificate2(Settings.CertificatePath);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Certificate", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        public static X509Certificate2 CreateCertificate(string certName, X509Certificate2 ca, int keyStrength)
        {
            var caCert = DotNetUtilities.FromX509Certificate(ca);
            var random = new SecureRandom(new CryptoApiRandomGenerator());
            var keyPairGen = new RsaKeyPairGenerator();
            keyPairGen.Init(new KeyGenerationParameters(random, keyStrength));
            AsymmetricCipherKeyPair keyPair = keyPairGen.GenerateKeyPair();

            var certificateGenerator = new X509V3CertificateGenerator();

            var CN = new X509Name("CN=" + certName);
            var SN = BigInteger.ProbablePrime(120, random);

            certificateGenerator.SetSerialNumber(SN);
            certificateGenerator.SetSubjectDN(CN);
            certificateGenerator.SetIssuerDN(caCert.IssuerDN);
            certificateGenerator.SetNotAfter(DateTime.MaxValue);
            certificateGenerator.SetNotBefore(DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0, 0)));
            certificateGenerator.SetPublicKey(keyPair.Public);
            certificateGenerator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(keyPair.Public));
            certificateGenerator.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifierStructure(caCert.GetPublicKey()));

            var caKeyPair = DotNetUtilities.GetKeyPair(ca.PrivateKey);

            ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA512WITHRSA", caKeyPair.Private, random);

            var certificate = certificateGenerator.Generate(signatureFactory);

            certificate.Verify(caCert.GetPublicKey());

            var certificate2 = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));
            certificate2.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);

            return certificate2;
        }

        public static X509Certificate2 CreateCertificateAuthority(string caName, int keyStrength)
        {
            var random = new SecureRandom(new CryptoApiRandomGenerator());
            var keyPairGen = new RsaKeyPairGenerator();
            keyPairGen.Init(new KeyGenerationParameters(random, keyStrength));
            AsymmetricCipherKeyPair keypair = keyPairGen.GenerateKeyPair();

            var certificateGenerator = new X509V3CertificateGenerator();

            var CN = new X509Name("CN=" + caName);
            var SN = BigInteger.ProbablePrime(120, random);

            certificateGenerator.SetSerialNumber(SN);
            certificateGenerator.SetSubjectDN(CN);
            certificateGenerator.SetIssuerDN(CN);
            certificateGenerator.SetNotAfter(DateTime.MaxValue);
            certificateGenerator.SetNotBefore(DateTime.UtcNow.Subtract(new TimeSpan(2, 0, 0, 0)));
            certificateGenerator.SetPublicKey(keypair.Public);
            certificateGenerator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(keypair.Public));
            certificateGenerator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));

            ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA512WITHRSA", keypair.Private, random);

            var certificate = certificateGenerator.Generate(signatureFactory);

            var certificate2 = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));
            certificate2.PrivateKey = DotNetUtilities.ToRSA(keypair.Private as RsaPrivateCrtKeyParameters);

            return certificate2;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(textBox1.Text)) return;

                button1.Text = "Please wait";
                button1.Enabled = false;

                string backup = Application.StartupPath + "\\BackupCertificate.zip";
                Settings.ServerCertificate = CreateCertificateAuthority(textBox1.Text, 4096);
                File.WriteAllBytes(Settings.CertificatePath, Settings.ServerCertificate.Export(X509ContentType.Pkcs12));

                using (ZipArchive archive = ZipFile.Open(backup, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(Settings.CertificatePath, Path.GetFileName(Settings.CertificatePath));
                }
                MessageBox.Show(this, "Created a ZIP backup (BackupCertificate.zip)", "Certificate backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                MessageBox.Show(this, "If you want to use an updated version of AsyncRAT, remember to copy+paste your certificate", "Certificate backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Certificate", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                button1.Text = "Ok";
                button1.Enabled = true;
            }
        }
    }
}
