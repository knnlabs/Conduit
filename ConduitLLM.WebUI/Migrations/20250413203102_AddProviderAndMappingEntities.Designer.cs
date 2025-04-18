﻿// <auto-generated />
using System;
using ConduitLLM.WebUI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ConduitLLM.WebUI.Migrations
{
    [DbContext(typeof(ConfigurationDbContext))]
    [Migration("20250413203102_AddProviderAndMappingEntities")]
    partial class AddProviderAndMappingEntities
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.4");

            modelBuilder.Entity("ConduitLLM.Configuration.Entities.VirtualKey", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AllowedModels")
                        .HasColumnType("TEXT");

                    b.Property<string>("BudgetDuration")
                        .HasMaxLength(20)
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("BudgetStartDate")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("CurrentSpend")
                        .HasPrecision(18, 8)
                        .HasColumnType("decimal(18, 8)");

                    b.Property<DateTime?>("ExpiresAt")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("KeyHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("TEXT");

                    b.Property<string>("KeyName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<decimal?>("MaxBudget")
                        .HasPrecision(18, 8)
                        .HasColumnType("decimal(18, 8)");

                    b.Property<string>("Metadata")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ExpiresAt");

                    b.HasIndex("IsEnabled");

                    b.HasIndex("KeyHash")
                        .IsUnique();

                    b.HasIndex("KeyName")
                        .IsUnique();

                    b.ToTable("VirtualKeys");
                });

            modelBuilder.Entity("ConduitLLM.WebUI.Data.DbModelProviderMapping", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ModelAlias")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderModelId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ModelAlias")
                        .IsUnique();

                    b.ToTable("ModelMappings");
                });

            modelBuilder.Entity("ConduitLLM.WebUI.Data.DbProviderCredentials", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ApiBase")
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("ApiKey")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<string>("ApiVersion")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ProviderName")
                        .IsUnique();

                    b.ToTable("ProviderCredentials");
                });

            modelBuilder.Entity("ConduitLLM.WebUI.Data.GlobalSetting", b =>
                {
                    b.Property<string>("Key")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Key");

                    b.ToTable("GlobalSettings");
                });
#pragma warning restore 612, 618
        }
    }
}
