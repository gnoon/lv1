function urlEncode(inputString, encodeAllCharacter) {
  var outputString = '';
  if (inputString != null) {
    for (var i=0; i<inputString.length; i++) {
      var charCode = inputString.charCodeAt(i);
      var tempText = "";
      if (charCode < 128) {
        if (encodeAllCharacter) {
          var hexVal = charCode.toString(16);
          outputString += '%' + (hexVal.length < 2 ? '0' : '') + hexVal.toUpperCase();  
        } else {
          outputString += String.fromCharCode(charCode);
        }

      } else if((charCode > 127) && (charCode < 2048)) {
        tempText += String.fromCharCode((charCode >> 6) | 192);
        tempText += String.fromCharCode((charCode & 63) | 128);
        outputString += escape(tempText);
      } else {
        tempText += String.fromCharCode((charCode >> 12) | 224);
        tempText += String.fromCharCode(((charCode >> 6) & 63) | 128);
        tempText += String.fromCharCode((charCode & 63) | 128);
        outputString += escape(tempText);
      }
    }
  }
  return outputString;
}

function roundValue(value, dec) {
    var result = Math.round(value * Math.pow(10, dec)) / Math.pow(10, dec);
    return result;
}

function convertDate(value, spliter, locale, informat, outformat) {
	if(value.length == 10 && value.indexOf("_") == -1) {
		var d = value.split(spliter);
		if(d.length == 3) {
			var dd = d[0];
			var mm = d[1];
			var yy = d[2];
			if(yy >= 2500) {
				yy = yy - 543;
			}
			return dd.concat(spliter, mm, spliter, yy);
		}
	}
	return value;
}

function trace(obj) {
  var win = (typeof dialogArguments != 'undefined') ? dialogArguments : window;
  var w = win.open("", "_blank");
  w.document.open();
  if(typeof(obj) == 'string') w.document.write(obj);
  else if(typeof(obj) == 'object') {
    for(i in obj) 
    w.document.write(i+" = "+obj[i]+"<br>\n");
  }
  w.document.close();
}