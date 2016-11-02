﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Foundatio.Repositories.Elasticsearch.Tests.Repositories.Configuration.Indexes;
using Foundatio.Repositories.Elasticsearch.Tests.Repositories.Configuration.Types;
using Foundatio.Repositories.Elasticsearch.Tests.Repositories.Models;
using Foundatio.Repositories.JsonPatch;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Nest;
using Xunit;
using Xunit.Abstractions;

namespace Foundatio.Repositories.Elasticsearch.Tests {
    public sealed class PipelineTests : ElasticRepositoryTestBase {
        private readonly EmployeeRepository _employeeRepository;

        public PipelineTests(ITestOutputHelper output) : base(output) {
            // configure type so pipeline is created.
            var employeeType = new EmployeeTypeWithWithPipeline(new EmployeeIndex(_configuration));
            employeeType.ConfigureAsync().GetAwaiter().GetResult();

            _employeeRepository = new EmployeeRepository(employeeType);
            RemoveDataAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task Add() {
            var employee = await _employeeRepository.AddAsync(EmployeeGenerator.Generate(name: "  BLAKE  "));
            Assert.NotNull(employee?.Id);

            var result = await _employeeRepository.GetByIdAsync(employee.Id);
            Assert.Equal("blake", result.Name);
        }

        [Fact]
        public async Task AddCollection() {
            var employees = new List<Employee> {
                EmployeeGenerator.Generate(name: "  BLAKE  "),
                EmployeeGenerator.Generate(name: "\tBLAKE  ")
            };
            await _employeeRepository.AddAsync(employees);

            var result = await _employeeRepository.GetByIdsAsync(employees.Select(e => e.Id));
            Assert.Equal(2, result.Count);
            Assert.True(result.All(e => String.Equals(e.Name, "blake")));
        }

        [Fact]
        public async Task SaveCollection() {
            var employee1 = EmployeeGenerator.Generate(id: ObjectId.GenerateNewId().ToString());
            var employee2 = EmployeeGenerator.Generate(id: ObjectId.GenerateNewId().ToString());
            await _employeeRepository.AddAsync(new List<Employee> { employee1, employee2 });

            employee1.Name = "  BLAKE  ";
            employee2.Name = "\tBLAKE  ";
            await _employeeRepository.SaveAsync(new List<Employee> { employee1, employee2 });
            var result = await _employeeRepository.GetByIdsAsync(new List<string> { employee1.Id, employee2.Id });
            Assert.Equal(2, result.Count);
            Assert.True(result.All(e => String.Equals(e.Name, "blake")));
        }

        [Fact]
        public async Task JsonPatch() {
            var employee = await _employeeRepository.AddAsync(EmployeeGenerator.Default);
            var patch = new PatchDocument(new ReplaceOperation { Path = "name", Value = "Patched" });
            await _employeeRepository.PatchAsync(employee.Id, patch);

            employee = await _employeeRepository.GetByIdAsync(employee.Id);
            Assert.Equal(EmployeeGenerator.Default.Age, employee.Age);
            Assert.Equal("patched", employee.Name);
            Assert.Equal(2, employee.Version);
        }

        [Fact]
        public async Task JsonPatchAll() {
            var utcNow = SystemClock.UtcNow;
            var employees = new List<Employee> {
                EmployeeGenerator.Generate(ObjectId.GenerateNewId(utcNow.AddDays(-1)).ToString(), createdUtc: utcNow.AddDays(-1), companyId: "1", yearsEmployed: 0),
                EmployeeGenerator.Generate(createdUtc: utcNow, companyId: "1", yearsEmployed: 0),
                EmployeeGenerator.Generate(createdUtc: utcNow, companyId: "2", yearsEmployed: 0),
            };

            await _employeeRepository.AddAsync(employees);

            await _client.RefreshAsync(Indices.All);
            var patch = new PatchDocument(new ReplaceOperation { Path = "name", Value = "Patched" });
            await _employeeRepository.PatchAsync(employees.Select(l => l.Id), patch);

            await _client.RefreshAsync(Indices.All);
            var results = await _employeeRepository.GetAllByCompanyAsync("1");
            Assert.Equal(2, results.Documents.Count);
            foreach (var document in results.Documents) {
                Assert.Equal("1", document.CompanyId);
                Assert.Equal("patched", document.Name);
            }
        }

        [Fact (Skip = "Not yet supported: https://github.com/elastic/elasticsearch/issues/17895")]
        public async Task PartialPatch() {
            var employee = await _employeeRepository.AddAsync(EmployeeGenerator.Default);
            await _employeeRepository.PatchAsync(employee.Id, new { name = "Patched" });

            employee = await _employeeRepository.GetByIdAsync(employee.Id);
            Assert.Equal(EmployeeGenerator.Default.Age, employee.Age);
            Assert.Equal("patched", employee.Name);
            Assert.Equal(2, employee.Version);
        }

        [Fact(Skip = "Not yet supported: https://github.com/elastic/elasticsearch/issues/17895")]
        public async Task PartialPatchAll() {
            var utcNow = SystemClock.UtcNow;
            var employees = new List<Employee> {
                EmployeeGenerator.Generate(ObjectId.GenerateNewId(utcNow.AddDays(-1)).ToString(), createdUtc: utcNow.AddDays(-1), companyId: "1", yearsEmployed: 0),
                EmployeeGenerator.Generate(createdUtc: utcNow, companyId: "1", yearsEmployed: 0),
                EmployeeGenerator.Generate(createdUtc: utcNow, companyId: "2", yearsEmployed: 0),
            };

            await _employeeRepository.AddAsync(employees);

            await _client.RefreshAsync(Indices.All);
            await _employeeRepository.PatchAsync(employees.Select(l => l.Id), new { name = "Patched" });

            await _client.RefreshAsync(Indices.All);
            var results = await _employeeRepository.GetAllByCompanyAsync("1");
            Assert.Equal(2, results.Documents.Count);
            foreach (var document in results.Documents) {
                Assert.Equal("1", document.CompanyId);
                Assert.Equal("patched", document.Name);
            }
        }

        [Fact(Skip = "Not yet supported: https://github.com/elastic/elasticsearch/issues/17895")]
        public async Task ScriptPatch() {
            var employee = await _employeeRepository.AddAsync(EmployeeGenerator.Default);
            await _employeeRepository.PatchAsync(employee.Id, "ctx._source.name = 'Patched';");

            employee = await _employeeRepository.GetByIdAsync(employee.Id);
            Assert.Equal(EmployeeGenerator.Default.Age, employee.Age);
            Assert.Equal("patched", employee.Name);
            Assert.Equal(2, employee.Version);
        }

        [Fact(Skip = "Not yet supported: https://github.com/elastic/elasticsearch/issues/17895")]
        public async Task ScriptPatchAll() {
            var utcNow = SystemClock.UtcNow;
            var employees = new List<Employee> {
                EmployeeGenerator.Generate(ObjectId.GenerateNewId(utcNow.AddDays(-1)).ToString(), createdUtc: utcNow.AddDays(-1), companyId: "1", yearsEmployed: 0),
                EmployeeGenerator.Generate(createdUtc: utcNow, companyId: "1", yearsEmployed: 0),
                EmployeeGenerator.Generate(createdUtc: utcNow, companyId: "2", yearsEmployed: 0),
            };

            await _employeeRepository.AddAsync(employees);

            await _client.RefreshAsync(Indices.All);
            await _employeeRepository.PatchAsync(employees.Select(l => l.Id), "ctx._source.name = 'Patched';");

            await _client.RefreshAsync(Indices.All);
            var results = await _employeeRepository.GetAllByCompanyAsync("1");
            Assert.Equal(2, results.Documents.Count);
            foreach (var document in results.Documents) {
                Assert.Equal("1", document.CompanyId);
                Assert.Equal("patched", document.Name);
            }
        }
    }
}