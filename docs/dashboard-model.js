(function (root, factory) {
  "use strict";
  var api = factory();
  if (typeof module === "object" && module.exports) { module.exports = api; }
  root.LodDashboardModel = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";
  var views = Object.freeze(["overview", "monsters", "npcs", "abilities", "items", "world", "changes", "download"]);
  function normalizeView(value) { return views.indexOf(value) >= 0 ? value : "overview"; }
  function isValidDashboardSnapshot(value) {
    if (!value || value.schemaVersion !== 2 || typeof value.generatedAt !== "string") { return false; }
    if (!value.roadmap || !Array.isArray(value.roadmap.current) || value.roadmap.current.length === 0) { return false; }
    return Boolean(value.verification && typeof value.verification.status === "string");
  }
  function findFlow(flows, id) { return flows.find(function (flow) { return flow.id === id; }) || flows[0] || null; }
  return Object.freeze({ views: views, normalizeView: normalizeView, isValidDashboardSnapshot: isValidDashboardSnapshot, findFlow: findFlow });
});
