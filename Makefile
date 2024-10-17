SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))
ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
CONFIGURATION ?= Release
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""
ADDITIONAL_ARGS ?= -p:ContinuousIntegrationBuild=true
CODECOV_ARGS ?= --collect:"XPlat Code Coverage" --results-directory $(ARTIFACT_PATH)

clean:
	rm -rf $(ARTIFACT_PATH)/*
	dotnet clean -c $(CONFIGURATION)

build: clean
	dotnet build -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

test: build
	dotnet test -c $(CONFIGURATION) $(ADDITIONAL_ARGS) $(CODECOV_ARGS)
	grep -h "<package name=" $(ARTIFACT_PATH)/**/coverage.cobertura.xml

build-nativeaot: test
	dotnet publish -f net8.0 -o $(ARTIFACT_PATH)/consoleapp/net8.0 samples/ConsoleApp
	dotnet publish -f net9.0 -o $(ARTIFACT_PATH)/consoleapp/net9.0 samples/ConsoleApp

test-nativeaot: build-nativeaot
	$(shell find $(ARTIFACT_PATH)/consoleapp/net8.0/ | grep -E 'ConsoleApp(\.exe)?$$')
	$(shell find $(ARTIFACT_PATH)/consoleapp/net9.0/ | grep -E 'ConsoleApp(\.exe)?$$')

pack: test-nativeaot
	dotnet pack -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

push:
	for nupkg in `ls $(ARTIFACT_PATH)/*.nupkg`; do \
		dotnet nuget push $$nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY); \
	done

.DEFAULT_GOAL := pack