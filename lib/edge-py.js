exports.getCompiler = function () {
	return process.env.EDGE_PY_NATIVE || (__dirname + '\\edge-py.dll');
};
