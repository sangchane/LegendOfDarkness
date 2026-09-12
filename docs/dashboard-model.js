(function (root, factory) {
  "use strict";
  var api = factory();
  if (typeof module === "object" && module.exports) { module.exports = api; }
  root.LodDashboardModel = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";
  var views = Object.freeze(["overview", "system", "flows", "delivery", "world", "abilities", "knowledge", "operations", "prototypes"]);
  function normalizeView(value) { return views.indexOf(value) >= 0 ? value : "overview"; }
  function isNullableBoolean(value) { return typeof value === "boolean" || value === null; }
  function isNullableCount(value) { return value === null || (Number.isInteger(value) && value >= 0); }
  function isValidDashboardSnapshot(value) {
    if (!value || value.schemaVersion !== 2 || typeof value.generatedAt !== "string") { return false; }
    if (!value.roadmap || !Array.isArray(value.roadmap.current) || value.roadmap.current.length === 0) { return false; }
    if (!value.git || typeof value.git.branch !== "string" || typeof value.git.sha !== "string" || !isNullableBoolean(value.git.dirty)) { return false; }
    if (!value.verification || typeof value.verification.status !== "string") { return false; }
    var graph = value.graphify;
    if (!graph || typeof graph.status !== "string" || !isNullableCount(graph.nodes) || !isNullableCount(graph.links) || !isNullableCount(graph.communities)) { return false; }
    return Boolean(graph.obsidian && typeof graph.obsidian.status === "string" && isNullableCount(graph.obsidian.notes) && typeof graph.obsidian.canvas === "boolean");
  }
  function filterKnowledge(entries, category, query) {
    var selected = category || "all";
    var needle = String(query || "").trim().toLocaleLowerCase("ko");
    return entries.filter(function (entry) {
      var matchesCategory = selected === "all" || entry.category === selected;
      var haystack = [entry.title, entry.label, entry.summary, entry.source, entry.next].join(" ").toLocaleLowerCase("ko");
      return matchesCategory && (!needle || haystack.indexOf(needle) >= 0);
    });
  }
  function findFlow(flows, id) { return flows.find(function (flow) { return flow.id === id; }) || flows[0] || null; }
  return Object.freeze({ views: views, normalizeView: normalizeView, isValidDashboardSnapshot: isValidDashboardSnapshot, filterKnowledge: filterKnowledge, findFlow: findFlow });
});
