using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.DTO;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace StudentService
{
	internal sealed class StudentService : StatelessService, IStudent
    {
		private readonly IDocument _documentService;
        public StudentService(StatelessServiceContext context)
            : base(context)
        {
			var serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
			{
				return new FabricTransportServiceRemotingClientFactory(
					new FabricTransportRemotingSettings
					{
						ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
					}, callbackClient);
			});

			var serviceUri = new Uri("fabric:/EduAnalyzer/DocumentService");

			_documentService = serviceProxyFactory.CreateServiceProxy<IDocument>(serviceUri, new ServicePartitionKey(0));
		}

		public async Task<bool> ProcessDocumentAsync(DocumentDTO document)
		{
			await _documentService.UploadDocumentAsync(new Document
			{
				FileName = document.FileName,
				Extension = document.Extension.ToString().ToLower(),
				ContentType = document.ContentType,
				UserId = document.UserId,
				Version = 1,
				Content = document.Content,
			});

			return true;
		}

		public async Task<bool> RollbackToPreviousVersionAsync(Guid userId, string fileName)
		{
			int latestVersion = await _documentService.FindLatestVersion(userId, fileName);

			if (latestVersion > 0)
			{
				return await _documentService.DeleteSpecificDocumentVersion(userId, fileName, latestVersion);
			}

			return false;
		}
		public async Task<bool> ProcessNewVersionAsync(DocumentDTO document)
		{
			int version = await _documentService.FindLatestVersion(document.UserId, document.FileName);

			version++;

			await _documentService.UploadNewVersionAsync(new Document
			{
				FileName = document.FileName,
				Extension = document.Extension.ToString().ToLower(),
				ContentType = document.ContentType,
				UserId = document.UserId,
				Content = document.Content,
				Version = version,
			});

			return true;
		}

		public async Task<bool> DeleteDocument(Guid userId, string fileName)
		{
			return await _documentService.DeleteDocument(userId, fileName);
		}

		public async Task<List<DocumentInfo>> GetDocumentsForStudent(Guid userId)
		{
			return await _documentService.GetDocumentsByUserId(userId);
		}

		public async Task<(byte[] content, string contentType)> DownloadDocument(DocumentInfo document, Guid userId)
		{
			return await _documentService.DownloadDocument(document, userId);
		}

		protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
			return new List<ServiceInstanceListener>
			{
				new ServiceInstanceListener(serviceContext =>
					new FabricTransportServiceRemotingListener(
						serviceContext,
						this,
						new FabricTransportRemotingListenerSettings
							{
								ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
							}),
						"ServiceEndpointV2")
			};
		}

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
