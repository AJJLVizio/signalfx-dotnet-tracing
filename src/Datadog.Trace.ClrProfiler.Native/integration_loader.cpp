#include "integration_loader.h"

namespace trace {

using json = nlohmann::json;

std::vector<integration> IntegrationLoader::LoadIntegrationsFromFile(
    const std::wstring& file_path) {
  std::vector<integration> integrations;

  try {
    std::ifstream stream;
    stream.open(file_path);
    integrations = LoadIntegrationsFromStream(stream);
    stream.close();
  } catch (...) {
    LOG_APPEND(L"failed to load integrations");
  }

  return integrations;
}

std::vector<integration> IntegrationLoader::LoadIntegrationsFromStream(
    std::istream& stream) {
  std::vector<integration> integrations;

  try {
    json j;
    // parse the stream
    stream >> j;

    for (auto& el : j) {
      auto i = IntegrationLoader::IntegrationFromJson(el);
      if (i.has_value()) {
        integrations.push_back(i.value());
      }
    }

    LOG_APPEND(L"loaded integrations: " << j.dump().c_str());
  } catch (const json::parse_error& e) {
    LOG_APPEND(L"invalid integrations: " << e.what());
  } catch (const json::type_error& e) {
    LOG_APPEND(L"invalid integrations: " << e.what());
  } catch (...) {
    LOG_APPEND(L"failed to load integrations");
  }

  return integrations;
}

std::optional<integration> IntegrationLoader::IntegrationFromJson(
    const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  // first get the name, which is required
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  std::wstring name = converter.from_bytes(src.value("name", ""));
  if (name.empty()) {
    LOG_APPEND(L"integration name is missing for integration: "
               << src.dump().c_str());
    return {};
  }

  std::vector<method_replacement> replacements;
  auto arr = src.value("method_replacements", json::array());
  if (arr.is_array()) {
    for (auto& el : arr) {
      auto mr = MethodReplacementFromJson(el);
      if (mr.has_value()) {
        replacements.push_back(mr.value());
      }
    }
  }
  return integration(IntegrationType_Custom, name, replacements);
}

std::optional<method_replacement> IntegrationLoader::MethodReplacementFromJson(
    const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  auto caller = IntegrationLoader::MethodReferenceFromJson(
      src.value("caller", json::object()));
  auto target = IntegrationLoader::MethodReferenceFromJson(
      src.value("target", json::object()));
  auto wrapper = IntegrationLoader::MethodReferenceFromJson(
      src.value("wrapper", json::object()));
  return method_replacement(caller, target, wrapper);
}

method_reference IntegrationLoader::MethodReferenceFromJson(
    const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  std::wstring assembly = converter.from_bytes(src.value("assembly", ""));
  std::wstring type = converter.from_bytes(src.value("type", ""));
  std::wstring method = converter.from_bytes(src.value("method", ""));
  return method_reference(assembly, type, method, {});
}

}  // namespace trace